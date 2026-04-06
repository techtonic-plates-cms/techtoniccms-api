using System.Linq.Expressions;
using TechtonicCmsApi.Schema.TechtonicCms.Entities;

namespace TechtonicCmsApi.Data;

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

    public IQueryable CreateQuery(Expression expression)
    {
        var elementType = expression.Type.GetGenericArguments()[0];
        var queryableType = typeof(CmsQueryable<>).MakeGenericType(elementType);
        return (IQueryable)Activator.CreateInstance(queryableType, this, expression)!;
    }

    public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
    {
        return new CmsQueryable<TElement>(this, expression);
    }

    public object Execute(Expression expression)
    {
        var rewritten = Rewrite(expression);
        return _innerSource.Provider.Execute(rewritten);
    }

    public TResult Execute<TResult>(Expression expression)
    {
        var rewritten = Rewrite(expression);
        return _innerSource.Provider.Execute<TResult>(rewritten);
    }

    internal Expression Rewrite(Expression expression)
    {
        return new CmsExpressionRewriter(_dynamicType, _mappings, _innerSource)
            .Visit(expression);
    }

    internal IQueryable<Entry> InnerSource => _innerSource;
    internal CmsFieldMapping[] Mappings => _mappings;
    internal Type DynamicType => _dynamicType;
}
