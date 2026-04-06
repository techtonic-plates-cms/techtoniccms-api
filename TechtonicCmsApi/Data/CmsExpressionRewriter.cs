using System.Collections.ObjectModel;
using System.Linq.Expressions;
using System.Reflection;
using TechtonicCmsApi.Schema.TechtonicCms.Entities;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;

namespace TechtonicCmsApi.Data;

/// <summary>
/// Translates a LINQ expression tree from dynamic-type space into Entry+JSONB space.
///
/// This is the core of Phase 3 — it bridges the gap between what HotChocolate thinks
/// it's querying (dynamic types like BlogPostsEntry) and what EF Core actually queries
/// (the Entry entity with a JSONB Data column).
///
/// Example transformation:
///   IN:  Queryable.Where&lt;BlogPostsEntry&gt;(
///          constant:CmsQueryable,
///          lambda: (BlogPostsEntry x) => x.Title == "Hello"
///        )
///   OUT: Queryable.Where&lt;Entry&gt;(
///          constant:IQueryable&lt;Entry&gt;,
///          lambda: (Entry x) => CmsObjectFunctions.ExtractText(x.Data, "Title") == "Hello"
///        )
///
/// The rewriter is invoked by CmsQueryProvider.Rewrite() right before expression execution.
/// A fresh instance is created per rewrite (no state reuse between rewrites).
///
/// Visitor visitation order matters:
///   1. VisitMethodCall (Queryable.Where/OrderBy/etc.) — visits children first
///   2. VisitConstant — replaces CmsQueryable root with IQueryable&lt;Entry&gt;
///   3. VisitLambda — creates new Entry parameters, stores in _parameterMap
///   4. VisitMember — translates property accesses to JSONB extraction calls
///   5. VisitParameter — maps old dynamic-type parameters to new Entry parameters
///   6. VisitUnary — handles Quote/Convert wrappers that HC inserts
/// </summary>
public class CmsExpressionRewriter : ExpressionVisitor
{
    /// <summary>
    /// The dynamic CLR type (e.g., BlogPostsEntry) used to identify parameters and constants
    /// that need rewriting. Any parameter or constant with this element type gets replaced.
    /// </summary>
    private readonly Type _dynamicType;

    /// <summary>
    /// Field mappings indexed by property name for O(1) lookup during VisitMember.
    /// Contains both base fields (IsBaseField=true, passthrough to Entry columns)
    /// and dynamic fields (IsBaseField=false, translated to JSONB extraction).
    /// </summary>
    private readonly Dictionary<string, CmsFieldMapping> _mappingByName;

    /// <summary>
    /// The real EF Core IQueryable&lt;Entry&gt; that replaces the CmsQueryable constant
    /// at the root of the expression tree. Typically pre-filtered by CollectionId
    /// (set up by Phase 4's query resolvers).
    /// </summary>
    private readonly IQueryable<Entry> _innerSource;

    /// <summary>
    /// Maps old dynamic-type parameters to new Entry-type parameters within a single lambda.
    /// Populated by VisitLambda, consumed by VisitParameter and VisitMember.
    ///
    /// Example: { x:Parameter(BlogPostsEntry) → x:Parameter(Entry) }
    ///
    /// This dictionary is scoped to the current rewrite operation. Since each rewrite
    /// creates a fresh CmsExpressionRewriter instance, there's no cross-rewrite contamination.
    /// </summary>
    private readonly Dictionary<ParameterExpression, ParameterExpression> _parameterMap = new();

    /// <summary>
    /// The 9 Queryable methods that HotChocolate is known to compose on IQueryable.
    /// Only these methods get the full reconstruction treatment (generic arg replacement).
    /// Any other Queryable method is passed through with visited children but no reconstruction.
    /// </summary>
    private static readonly HashSet<string> TargetQueryableMethods =
    [
        "Where", "OrderBy", "OrderByDescending", "ThenBy", "ThenByDescending",
        "Skip", "Take", "Count", "Any"
    ];

    // Pre-cached MethodInfo references for the 4 CmsObjectFunctions extraction stubs.
    // Used by BuildJsonbExtraction to construct Expression.Call nodes.
    // These methods are never actually invoked — EF Core replaces them with SQL function calls
    // during query compilation because they're annotated with [DbFunction].
    private static readonly MethodInfo ExtractTextMethod =
        typeof(CmsObjectFunctions).GetMethod(nameof(CmsObjectFunctions.ExtractText))!;

    private static readonly MethodInfo ExtractNumberMethod =
        typeof(CmsObjectFunctions).GetMethod(nameof(CmsObjectFunctions.ExtractNumber))!;

    private static readonly MethodInfo ExtractBooleanMethod =
        typeof(CmsObjectFunctions).GetMethod(nameof(CmsObjectFunctions.ExtractBoolean))!;

    private static readonly MethodInfo ExtractDateTimeMethod =
        typeof(CmsObjectFunctions).GetMethod(nameof(CmsObjectFunctions.ExtractDateTime))!;

    /// <summary>
    /// Cached PropertyInfo for Entry.Data — used to build the first argument of
    /// extraction calls: CmsObjectFunctions.ExtractText(x.Data, "key").
    /// </summary>
    private static readonly PropertyInfo EntryDataProperty =
        typeof(Entry).GetProperty(nameof(Entry.Data))!;

    public CmsExpressionRewriter(
        Type dynamicType,
        IReadOnlyList<CmsFieldMapping> mappings,
        IQueryable<Entry> innerSource)
    {
        _dynamicType = dynamicType;
        _innerSource = innerSource;
        _mappingByName = new Dictionary<string, CmsFieldMapping>(mappings.Count);
        foreach (var m in mappings)
            _mappingByName[m.PropertyName] = m;
    }

    /// <summary>
    /// Replaces CmsQueryable&lt;T&gt; constant nodes with the inner IQueryable&lt;Entry&gt;.
    ///
    /// This is the anchor replacement — the root of every HC-composed expression tree is
    /// a ConstantExpression wrapping the CmsQueryable. Everything else (Where, OrderBy, etc.)
    /// chains off this root. By replacing it, we swap the entire data source from
    /// dynamic-type space to Entry space.
    ///
    /// The explicit type parameter (typeof(IQueryable&lt;Entry&gt;)) is critical — without it,
    /// Expression.Constant would use the runtime type (EntityQueryable&lt;Entry&gt;), which is
    /// an internal EF Core type that method call resolution might not handle correctly.
    /// </summary>
    protected override Expression VisitConstant(ConstantExpression node)
    {
        if (node.Value is IQueryable q && q.ElementType == _dynamicType)
            return Expression.Constant(_innerSource, typeof(IQueryable<Entry>));

        return node;
    }

    /// <summary>
    /// Rewrites lambda parameters from dynamic type to Entry type.
    ///
    /// This MUST run before the lambda body is visited, because VisitMember and
    /// VisitParameter depend on _parameterMap being populated. The sequence is:
    ///   1. Check if any parameters need remapping (type == _dynamicType)
    ///   2. Create new Entry-typed parameters, store in _parameterMap
    ///   3. Visit the body (which uses _parameterMap via VisitParameter/VisitMember)
    ///   4. Construct new lambda with Entry parameters
    ///
    /// For lambdas whose parameters are NOT the dynamic type (e.g., inner lambdas
    /// in nested expressions), we still visit the body but keep the original parameters.
    /// </summary>
    protected override Expression VisitLambda<T>(Expression<T> node)
    {
        // Phase 1: Check if any parameters reference the dynamic type.
        var needsRemap = false;
        foreach (var p in node.Parameters)
        {
            if (p.Type == _dynamicType)
            {
                needsRemap = true;
                break;
            }
        }

        // Fast path: no dynamic-type parameters → just visit the body, keep original params.
        if (!needsRemap)
        {
            var visitedBody = Visit(node.Body);
            if (visitedBody == node.Body)
                return node;
            return Expression.Lambda<T>(visitedBody, node.Name, node.TailCall, node.Parameters);
        }

        // Phase 2: Create new Entry-typed parameters and store mappings.
        // Preserves parameter names for debugging readability.
        var newParams = new ParameterExpression[node.Parameters.Count];
        for (var i = 0; i < node.Parameters.Count; i++)
        {
            var param = node.Parameters[i];
            if (param.Type == _dynamicType)
            {
                var newParam = Expression.Parameter(typeof(Entry), param.Name);
                _parameterMap[param] = newParam;
                newParams[i] = newParam;
            }
            else
            {
                newParams[i] = param;
            }
        }

        // Phase 3: Visit the body — this triggers VisitMember/VisitParameter rewrites
        // that use _parameterMap to translate dynamic property accesses to JSONB calls.
        var body = Visit(node.Body);

        // Phase 4: Construct new lambda with Entry parameters.
        // Note: we use Expression.Lambda (non-generic) instead of Expression.Lambda<T>
        // because the parameter types have changed — the generic T signature no longer matches.
        return Expression.Lambda(body, node.Name, node.TailCall, newParams);
    }

    /// <summary>
    /// Maps old dynamic-type parameter references to their new Entry-type replacements.
    /// Simple dictionary lookup — the mapping was established by VisitLambda.
    /// Parameters not in the map (e.g., captured outer variables) pass through unchanged.
    /// </summary>
    protected override Expression VisitParameter(ParameterExpression node)
    {
        return _parameterMap.TryGetValue(node, out var mapped) ? mapped : base.VisitParameter(node);
    }

    /// <summary>
    /// Translates property accesses on the dynamic type.
    ///
    /// This is where the JSONB magic happens. For a member access like x.Title where
    /// x has been remapped to type Entry:
    ///
    ///   Base field (x.Id, x.Name, etc.):
    ///     → Expression.Property(entryExpr, "Id") — Entry actually has these properties,
    ///       so we just redirect to the correct property on Entry.
    ///
    ///   Dynamic field (x.Title, x.Views, etc.):
    ///     → BuildJsonbExtraction(entryExpr, mapping) — constructs a call like
    ///       CmsObjectFunctions.ExtractText(entryExpr.Data, "Title")
    ///
    /// We visit the member's expression first (the object being accessed) because it might
    /// be a parameter that needs remapping. After visiting, if it's now of type Entry and
    /// the member name matches a known mapping, we perform the translation.
    ///
    /// If the member isn't in our mapping (shouldn't happen but defensive), we fall through
    /// to the default member access behavior.
    /// </summary>
    protected override Expression VisitMember(MemberExpression node)
    {
        // Static members have null Expression — nothing to rewrite.
        if (node.Expression is null)
            return node;

        // Visit the object expression first — this may remap parameters.
        var visitedExpr = Visit(node.Expression);

        // After visiting, if the expression is now Entry-typed and we have a mapping
        // for this property name, perform the translation.
        if (visitedExpr.Type == typeof(Entry)
            && _mappingByName.TryGetValue(node.Member.Name, out var mapping))
        {
            return mapping.IsBaseField
                ? Expression.Property(visitedExpr, mapping.PropertyName)
                : BuildJsonbExtraction(visitedExpr, mapping);
        }

        // No mapping found or expression isn't Entry-typed.
        // If nothing changed, return original to preserve reference identity.
        if (visitedExpr == node.Expression)
            return node;

        // Something changed upstream — reconstruct with visited expression.
        return Expression.MakeMemberAccess(visitedExpr, node.Member);
    }

    /// <summary>
    /// Handles Queryable static method calls (Where, OrderBy, Skip, Take, Count, etc.)
    /// by visiting all children first, then reconstructing the method if the element type
    /// changed from dynamic to Entry.
    ///
    /// The reconstruction is necessary because Queryable.Where&lt;BlogPostsEntry&gt; becomes
    /// Queryable.Where&lt;Entry&gt; when the source changes from CmsQueryable to IQueryable&lt;Entry&gt;.
    /// We can't just swap arguments — the method's generic type parameter must also change.
    ///
    /// For non-Queryable methods (e.g., string.Contains, Nullable.HasValue), we just pass
    /// through with visited children using node.Update.
    /// </summary>
    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        // Visit the instance (null for static methods like Queryable.Where).
        var visitedObj = Visit(node.Object);

        // Visit all arguments — this is where lambda bodies get rewritten.
        var visitedArgs = new Expression[node.Arguments.Count];
        for (var i = 0; i < node.Arguments.Count; i++)
            visitedArgs[i] = Visit(node.Arguments[i]);

        // If this is a Queryable method HC uses, apply full reconstruction logic.
        if (node.Method.DeclaringType == typeof(Queryable)
            && TargetQueryableMethods.Contains(node.Method.Name))
        {
            return ReconstructQueryableMethod(node, visitedObj, visitedArgs);
        }

        // For all other method calls, return unchanged if nothing was actually modified.
        // Reference equality check avoids unnecessary expression tree churn.
        if (visitedObj == node.Object && ArgsEqual(visitedArgs, node.Arguments))
            return node;

        // Reconstruct with visited children but same method.
        return node.Update(visitedObj, visitedArgs);
    }

    /// <summary>
    /// Handles unary expressions — Quote and Convert are the two HC inserts frequently.
    ///
    /// Quote: HC wraps lambda arguments in Quote nodes (e.g., in Queryable.Where's second
    ///   argument). The Quote must be preserved because it signals "this is an expression tree,
    ///   not a delegate invocation." We re-quote the visited operand.
    ///
    /// Convert: HC inserts type conversions (e.g., Convert(x.Views, Double)). After rewriting,
    ///   the operand may have changed type (e.g., from double to double?). If the visited
    ///   operand's type already matches the target type, we strip the Convert to avoid
    ///   redundant/invalid casts. Otherwise we preserve it.
    /// </summary>
    protected override Expression VisitUnary(UnaryExpression node)
    {
        var visitedOperand = Visit(node.Operand);

        if (visitedOperand == node.Operand)
            return node;

        return node.NodeType switch
        {
            ExpressionType.Quote => Expression.Quote(visitedOperand),
            // Strip redundant converts — if the rewritten operand already has the target type,
            // keeping the Convert would be harmless for value types but can cause issues with
            // nullable type conversions (e.g., Convert(double?, Double) is invalid).
            ExpressionType.Convert when visitedOperand.Type == node.Type => visitedOperand,
            ExpressionType.Convert => Expression.Convert(visitedOperand, node.Type),
            _ => node.Update(visitedOperand)
        };
    }

    /// <summary>
    /// Constructs a CmsObjectFunctions.Extract*() call expression for a dynamic field.
    ///
    /// Produces the expression tree equivalent of:
    ///   CmsObjectFunctions.ExtractText(x.Data, "Title")
    ///
    /// The dataAccess expression (x.Data) uses the Entry.Data property, and the key argument
    /// is a string constant from the field mapping. The method is selected based on DataType:
    ///   Text/RichText/Relation/Asset → ExtractText (→ string?)
    ///   Number                       → ExtractNumber (→ double?)
    ///   Boolean                      → ExtractBoolean (→ bool?)
    ///   DateTime                     → ExtractDateTime (→ DateTime?)
    ///   TextList/NumberList/Object   → throws NotSupportedException
    ///
    /// TextList/NumberList/Object fields cannot participate in server-side filtering/sorting
    /// because we don't have JSONB array/object query functions. Phase 4 configures HC's
    /// schema to not expose filter/sort inputs for these types, so this exception should
    /// never be hit in normal operation.
    /// </summary>
    private Expression BuildJsonbExtraction(Expression entryExpr, CmsFieldMapping mapping)
    {
        // Build: entryExpr.Data
        var dataAccess = Expression.Property(entryExpr, EntryDataProperty);

        // Build: "Title" (or whatever the JSONB key is)
        var keyArg = Expression.Constant(mapping.JsonKey);

        // Select the correct extraction method based on the field's data type.
        var method = mapping.DataType switch
        {
            FieldDataType.Text or FieldDataType.RichText or FieldDataType.Relation or FieldDataType.Asset
                => ExtractTextMethod,
            FieldDataType.Number => ExtractNumberMethod,
            FieldDataType.Boolean => ExtractBooleanMethod,
            FieldDataType.DateTime => ExtractDateTimeMethod,
            FieldDataType.TextList or FieldDataType.NumberList or FieldDataType.Object
                => throw new NotSupportedException(
                    $"Filtering/sorting on '{mapping.PropertyName}' ({mapping.DataType}) is not supported. " +
                    "JSONB array/object fields cannot be used in queries."),
            _ => throw new NotSupportedException($"Unknown data type for field '{mapping.PropertyName}': {mapping.DataType}")
        };

        // Build: CmsObjectFunctions.ExtractText(entryExpr.Data, "Title")
        return Expression.Call(method, dataAccess, keyArg);
    }

    /// <summary>
    /// Reconstructs a Queryable method call when the element type has changed from dynamic to Entry.
    ///
    /// This is needed because HC composes methods like Queryable.Where&lt;BlogPostsEntry&gt;(source, lambda),
    /// but after rewriting the source becomes IQueryable&lt;Entry&gt;. We must change the generic
    /// type parameter to Entry so the expression tree is internally consistent.
    ///
    /// Single-generic-arg methods (Where, Skip, Take, Count, Any):
    ///   Queryable.Where&lt;BlogPostsEntry&gt; → Queryable.Where&lt;Entry&gt;
    ///   Just swap T to the new element type.
    ///
    /// Dual-generic-arg methods (OrderBy, ThenBy, etc.):
    ///   Queryable.OrderBy&lt;BlogPostsEntry, string&gt; → Queryable.OrderBy&lt;Entry, string?&gt;
    ///   T changes to Entry, K changes to the visited lambda's return type.
    ///   The key type may change (e.g., string → string? because ExtractText returns string?),
    ///   so we derive K from the ACTUAL visited lambda rather than the original.
    ///
    /// If the element type DIDN'T change (e.g., the expression was already in Entry space),
    /// we skip reconstruction and just update with visited children.
    /// </summary>
    private Expression ReconstructQueryableMethod(
        MethodCallExpression original,
        Expression? visitedObj,
        Expression[] visitedArgs)
    {
        var originalGenericArgs = original.Method.GetGenericArguments();
        var originalElementType = originalGenericArgs[0];

        // Determine the new element type from the visited source argument.
        // This walks through IQueryable<T> / IOrderedQueryable<T> to extract T.
        var newElementType = GetQueryableElementType(visitedArgs[0].Type);

        // If element type didn't change, no reconstruction needed.
        if (newElementType == originalElementType)
        {
            if (visitedObj == original.Object && ArgsEqual(visitedArgs, original.Arguments))
                return original;
            return original.Update(visitedObj, visitedArgs);
        }

        // Reconstruct with new generic type arguments.
        MethodInfo newMethod;
        if (originalGenericArgs.Length == 1)
        {
            // Single-generic-arg: Where<T>, Skip<T>, Take<T>, Count<T>, Any<T>
            newMethod = original.Method.GetGenericMethodDefinition()
                .MakeGenericMethod(newElementType);
        }
        else if (originalGenericArgs.Length == 2)
        {
            // Dual-generic-arg: OrderBy<T,K>, OrderByDescending<T,K>, ThenBy<T,K>, ThenByDescending<T,K>
            // T changes to Entry, K is derived from the visited lambda's return type.
            // We unwrap Quote nodes (HC wraps lambdas in quotes) to get the actual LambdaExpression.
            var lambda = UnquoteLambda(visitedArgs[1]);
            newMethod = original.Method.GetGenericMethodDefinition()
                .MakeGenericMethod(newElementType, lambda.ReturnType);
        }
        else
        {
            // Unknown arity — fall back to update with visited children.
            return original.Update(visitedObj, visitedArgs);
        }

        return Expression.Call(visitedObj, newMethod, visitedArgs);
    }

    /// <summary>
    /// Unwraps a Quote node around a lambda, if present.
    ///
    /// HC wraps lambda arguments in Quote nodes because they're expression trees, not delegates.
    /// For example, Queryable.Where's second argument is Quote(lambda). We need the inner
    /// LambdaExpression to read its ReturnType for generic method reconstruction.
    /// </summary>
    private static LambdaExpression UnquoteLambda(Expression expr)
    {
        if (expr is UnaryExpression { NodeType: ExpressionType.Quote } quote)
            return (LambdaExpression)quote.Operand;
        return (LambdaExpression)expr;
    }

    /// <summary>
    /// Extracts the element type T from an IQueryable&lt;T&gt; type.
    ///
    /// Handles both direct IQueryable&lt;T&gt; types and types that implement IQueryable&lt;T&gt;
    /// indirectly (e.g., IOrderedQueryable&lt;T&gt;, EntityQueryable&lt;T&gt;).
    /// The latter requires scanning implemented interfaces.
    /// </summary>
    private static Type GetQueryableElementType(Type type)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IQueryable<>))
            return type.GetGenericArguments()[0];

        foreach (var iface in type.GetInterfaces())
        {
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IQueryable<>))
                return iface.GetGenericArguments()[0];
        }

        return type;
    }

    /// <summary>
    /// Reference-equality comparison between visited and original argument arrays.
    /// Used to avoid unnecessary expression tree reconstruction when nothing changed.
    /// </summary>
    private static bool ArgsEqual(Expression[] visited, ReadOnlyCollection<Expression> original)
    {
        if (visited.Length != original.Count)
            return false;
        for (var i = 0; i < visited.Length; i++)
        {
            if (!ReferenceEquals(visited[i], original[i]))
                return false;
        }
        return true;
    }
}
