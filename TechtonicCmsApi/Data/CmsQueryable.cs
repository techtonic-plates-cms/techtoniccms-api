using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using TechtonicCmsApi.Schema.TechtonicCms.Entities;

namespace TechtonicCmsApi.Data;

public class CmsQueryable<T> : IQueryable<T>, IOrderedQueryable<T>, IAsyncEnumerable<T>
{
    private readonly CmsQueryProvider _provider;
    private readonly Expression _expression;
    private readonly IQueryable<Entry> _innerSource;
    private readonly CmsFieldMapping[] _mappings;

    public CmsQueryable(IQueryable<Entry> innerSource, CmsFieldMapping[] mappings)
    {
        _innerSource = innerSource;
        _mappings = mappings;
        _provider = new CmsQueryProvider(innerSource, mappings, typeof(T));
        _expression = Expression.Constant(this, typeof(IQueryable<T>));
    }

    public CmsQueryable(CmsQueryProvider provider, Expression expression)
    {
        _provider = provider;
        _expression = expression;
        _innerSource = provider.InnerSource;
        _mappings = provider.Mappings;
    }

    public Type ElementType => typeof(T);
    public Expression Expression => _expression;
    public IQueryProvider Provider => _provider;

    public IEnumerator<T> GetEnumerator()
    {
        return Enumerate().GetEnumerator();
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        return EnumerateAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);
    }

    private IEnumerable<T> Enumerate()
    {
        var rewritten = _provider.Rewrite(_expression);
        var entryQuery = _innerSource.Provider.CreateQuery<Entry>(rewritten);

        foreach (var entry in entryQuery)
            yield return CmsObjectMaterializer.Materialize<T>(entry, _mappings);
    }

    private async IAsyncEnumerable<T> EnumerateAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var rewritten = _provider.Rewrite(_expression);
        var entryQuery = _innerSource.Provider.CreateQuery<Entry>(rewritten);

        if (entryQuery is IAsyncEnumerable<Entry> asyncEntries)
        {
            await foreach (var entry in asyncEntries
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
            {
                yield return CmsObjectMaterializer.Materialize<T>(entry, _mappings);
            }
        }
        else
        {
            foreach (var entry in entryQuery)
                yield return CmsObjectMaterializer.Materialize<T>(entry, _mappings);
        }
    }
}
