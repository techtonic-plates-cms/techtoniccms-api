using System.Linq.Expressions;
using TechtonicCmsApi.Schema.TechtonicCms.Entities;

namespace TechtonicCmsApi.Data;

/// <summary>
/// Custom IQueryProvider that sits between HotChocolate and EF Core.
///
/// When HC composes LINQ operations (Where, OrderBy, Skip, Take) on a CmsQueryable,
/// those operations build up an expression tree against the dynamic CLR type.
/// CmsQueryProvider intercepts execution by:
///
///   1. CreateQuery&lt;T&gt; — wraps each HC-composed operation in a new CmsQueryable.
///      The expression tree grows but nothing is executed yet. HC chains multiple
///      CreateQuery calls as it applies filtering, sorting, and paging.
///
///   2. Execute&lt;TResult&gt; — called for scalar operations (Count, Any).
///      Rewrites the expression tree via CmsExpressionRewriter, then delegates to
///      EF Core's EntityQueryProvider for actual SQL execution.
///
/// The provider holds immutable references to:
///   - _innerSource: the original IQueryable&lt;Entry&gt; from EF Core (pre-filtered by CollectionId)
///   - _mappings: field metadata from CmsTypeFactory for property→JSONB translation
///   - _dynamicType: the emitted CLR type (e.g., BlogPostsEntry) for identifying what to rewrite
///
/// These are set once at construction (by CmsQueryable's root constructor) and shared
/// across all CmsQueryable instances created by CreateQuery during a single HC pipeline execution.
/// </summary>
public class CmsQueryProvider : IQueryProvider
{
    private readonly IQueryable<Entry> _innerSource;
    private readonly CmsFieldMapping[] _mappings;
    private readonly Type _dynamicType;

    public CmsQueryProvider(
        IQueryable<Entry> innerSource,
        CmsFieldMapping[] mappings,
        Type dynamicType)
    {
        _innerSource = innerSource;
        _mappings = mappings;
        _dynamicType = dynamicType;
    }

    /// <summary>
    /// Non-generic CreateQuery — called when HC doesn't have compile-time type information.
    /// Extracts the element type from the expression's type and constructs CmsQueryable
    /// via reflection. This path is used by HC's internal middleware in some cases.
    /// </summary>
    public IQueryable CreateQuery(Expression expression)
    {
        var elementType = expression.Type.GetGenericArguments()[0];
        var queryableType = typeof(CmsQueryable<>).MakeGenericType(elementType);
        return (IQueryable)Activator.CreateInstance(queryableType, this, expression)!;
    }

    /// <summary>
    /// Generic CreateQuery — the primary path used by HC when composing LINQ operations.
    ///
    /// Each call to Queryable.Where(), Queryable.OrderBy(), etc. on a CmsQueryable triggers
    /// this method, which wraps the new expression in another CmsQueryable sharing the same
    /// provider. The expression tree accumulates operations without executing anything.
    ///
    /// The element type TElement may differ from the original dynamic type when HC's
    /// paging middleware restructures the query (e.g., for Select projections), but
    /// typically stays the same throughout the filter/sort/page pipeline.
    /// </summary>
    public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
    {
        return new CmsQueryable<TElement>(this, expression);
    }

    /// <summary>
    /// Non-generic Execute — delegates to the generic version via reflection.
    /// Used for scalar queries where the return type isn't known at compile time.
    /// </summary>
    public object Execute(Expression expression)
    {
        var rewritten = Rewrite(expression);
        return _innerSource.Provider.Execute(rewritten);
    }

    /// <summary>
    /// Executes a scalar query (Count, Any) against the database.
    ///
    /// This is the path triggered by HC's CountAsync() → Task.Run(() => source.Count()).
    /// HC calls the synchronous Queryable.Count() method, which flows through
    /// IQueryProvider.Execute&lt;int&gt;.
    ///
    /// The flow:
    ///   1. Rewrite the expression tree (dynamic type → Entry + JSONB)
    ///   2. Delegate to EF Core's EntityQueryProvider.Execute&lt;TResult&gt;
    ///   3. EF Core compiles to SQL and executes synchronously
    ///
    /// No materialization happens here — the result is a scalar (int, bool).
    /// </summary>
    public TResult Execute<TResult>(Expression expression)
    {
        var rewritten = Rewrite(expression);
        return _innerSource.Provider.Execute<TResult>(rewritten);
    }

    /// <summary>
    /// Creates a fresh CmsExpressionRewriter and visits the entire expression tree.
    ///
    /// Called from two places:
    ///   - CmsQueryable.Enumerate/EnumerateAsync — for data retrieval (ToList path)
    ///   - CmsQueryProvider.Execute — for scalar queries (Count/Any path)
    ///
    /// A new rewriter instance is created each time to ensure clean _parameterMap state.
    /// The rewrite is a single-pass walk — O(n) in expression tree size.
    /// </summary>
    internal Expression Rewrite(Expression expression)
    {
        return new CmsExpressionRewriter(_dynamicType, _mappings, _innerSource)
            .Visit(expression);
    }

    /// <summary>
    /// Exposes the inner EF Core queryable for CmsQueryable's composed constructor
    /// and for creating rewritten queries during enumeration.
    /// </summary>
    internal IQueryable<Entry> InnerSource => _innerSource;

    /// <summary>
    /// Exposes field mappings for CmsQueryable's Enumerate/EnumerateAsync methods,
    /// which pass them to CmsObjectMaterializer.Materialize for row projection.
    /// </summary>
    internal CmsFieldMapping[] Mappings => _mappings;

    /// <summary>
    /// Exposes the dynamic type for CmsQueryable's composed constructor.
    /// </summary>
    internal Type DynamicType => _dynamicType;
}
