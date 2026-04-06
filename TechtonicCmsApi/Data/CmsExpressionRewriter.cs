using System.Collections.ObjectModel;
using System.Linq.Expressions;
using System.Reflection;
using TechtonicCmsApi.Schema.TechtonicCms.Entities;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;

namespace TechtonicCmsApi.Data;

public class CmsExpressionRewriter : ExpressionVisitor
{
    private readonly Type _dynamicType;
    private readonly Dictionary<string, CmsFieldMapping> _mappingByName;
    private readonly IQueryable<Entry> _innerSource;
    private readonly Dictionary<ParameterExpression, ParameterExpression> _parameterMap = new();

    private static readonly HashSet<string> TargetQueryableMethods =
    [
        "Where", "OrderBy", "OrderByDescending", "ThenBy", "ThenByDescending",
        "Skip", "Take", "Count", "Any"
    ];

    private static readonly MethodInfo ExtractTextMethod =
        typeof(CmsObjectFunctions).GetMethod(nameof(CmsObjectFunctions.ExtractText))!;

    private static readonly MethodInfo ExtractNumberMethod =
        typeof(CmsObjectFunctions).GetMethod(nameof(CmsObjectFunctions.ExtractNumber))!;

    private static readonly MethodInfo ExtractBooleanMethod =
        typeof(CmsObjectFunctions).GetMethod(nameof(CmsObjectFunctions.ExtractBoolean))!;

    private static readonly MethodInfo ExtractDateTimeMethod =
        typeof(CmsObjectFunctions).GetMethod(nameof(CmsObjectFunctions.ExtractDateTime))!;

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

    protected override Expression VisitConstant(ConstantExpression node)
    {
        if (node.Value is IQueryable q && q.ElementType == _dynamicType)
            return Expression.Constant(_innerSource, typeof(IQueryable<Entry>));

        return node;
    }

    protected override Expression VisitLambda<T>(Expression<T> node)
    {
        var needsRemap = false;
        foreach (var p in node.Parameters)
        {
            if (p.Type == _dynamicType)
            {
                needsRemap = true;
                break;
            }
        }

        if (!needsRemap)
        {
            var visitedBody = Visit(node.Body);
            if (visitedBody == node.Body)
                return node;
            return Expression.Lambda<T>(visitedBody, node.Name, node.TailCall, node.Parameters);
        }

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

        var body = Visit(node.Body);
        return Expression.Lambda(body, node.Name, node.TailCall, newParams);
    }

    protected override Expression VisitParameter(ParameterExpression node)
    {
        return _parameterMap.TryGetValue(node, out var mapped) ? mapped : base.VisitParameter(node);
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        if (node.Expression is null)
            return node;

        var visitedExpr = Visit(node.Expression);

        if (visitedExpr.Type == typeof(Entry)
            && _mappingByName.TryGetValue(node.Member.Name, out var mapping))
        {
            return mapping.IsBaseField
                ? Expression.Property(visitedExpr, mapping.PropertyName)
                : BuildJsonbExtraction(visitedExpr, mapping);
        }

        if (visitedExpr == node.Expression)
            return node;

        return Expression.MakeMemberAccess(visitedExpr, node.Member);
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        var visitedObj = Visit(node.Object);

        var visitedArgs = new Expression[node.Arguments.Count];
        for (var i = 0; i < node.Arguments.Count; i++)
            visitedArgs[i] = Visit(node.Arguments[i]);

        if (node.Method.DeclaringType == typeof(Queryable)
            && TargetQueryableMethods.Contains(node.Method.Name))
        {
            return ReconstructQueryableMethod(node, visitedObj, visitedArgs);
        }

        if (visitedObj == node.Object && ArgsEqual(visitedArgs, node.Arguments))
            return node;

        return node.Update(visitedObj, visitedArgs);
    }

    protected override Expression VisitUnary(UnaryExpression node)
    {
        var visitedOperand = Visit(node.Operand);

        if (visitedOperand == node.Operand)
            return node;

        return node.NodeType switch
        {
            ExpressionType.Quote => Expression.Quote(visitedOperand),
            ExpressionType.Convert when visitedOperand.Type == node.Type => visitedOperand,
            ExpressionType.Convert => Expression.Convert(visitedOperand, node.Type),
            _ => node.Update(visitedOperand)
        };
    }

    private Expression BuildJsonbExtraction(Expression entryExpr, CmsFieldMapping mapping)
    {
        var dataAccess = Expression.Property(entryExpr, EntryDataProperty);
        var keyArg = Expression.Constant(mapping.JsonKey);

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

        return Expression.Call(method, dataAccess, keyArg);
    }

    private Expression ReconstructQueryableMethod(
        MethodCallExpression original,
        Expression? visitedObj,
        Expression[] visitedArgs)
    {
        var originalGenericArgs = original.Method.GetGenericArguments();
        var originalElementType = originalGenericArgs[0];
        var newElementType = GetQueryableElementType(visitedArgs[0].Type);

        if (newElementType == originalElementType)
        {
            if (visitedObj == original.Object && ArgsEqual(visitedArgs, original.Arguments))
                return original;
            return original.Update(visitedObj, visitedArgs);
        }

        MethodInfo newMethod;
        if (originalGenericArgs.Length == 1)
        {
            newMethod = original.Method.GetGenericMethodDefinition()
                .MakeGenericMethod(newElementType);
        }
        else if (originalGenericArgs.Length == 2)
        {
            var lambda = UnquoteLambda(visitedArgs[1]);
            newMethod = original.Method.GetGenericMethodDefinition()
                .MakeGenericMethod(newElementType, lambda.ReturnType);
        }
        else
        {
            return original.Update(visitedObj, visitedArgs);
        }

        return Expression.Call(visitedObj, newMethod, visitedArgs);
    }

    private static LambdaExpression UnquoteLambda(Expression expr)
    {
        if (expr is UnaryExpression { NodeType: ExpressionType.Quote } quote)
            return (LambdaExpression)quote.Operand;
        return (LambdaExpression)expr;
    }

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
