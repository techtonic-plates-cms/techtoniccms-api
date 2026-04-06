using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using TechtonicCmsApi.Schema.TechtonicCms.Entities;

namespace TechtonicCmsApi.Data;

/// <summary>
/// The IQueryable that HotChocolate composes filtering, sorting, and paging against.
///
/// This is the public-facing type returned by Phase 4's query resolvers. HC sees it as
/// a regular queryable and chains .Where(), .OrderBy(), .Skip(), .Take() on it without
/// knowing that underneath it translates property accesses to PostgreSQL JSONB function calls.
///
/// Implements three interfaces:
///   - IQueryable&lt;T&gt; — HC uses Provider.CreateQuery to chain operations
///   - IOrderedQueryable&lt;T&gt; — marker so ThenBy/ThenByDescending compile after OrderBy
///   - IAsyncEnumerable&lt;T&gt; — HC's ToListAsync checks for this and iterates via await foreach
///
/// Two construction paths:
///   1. Root constructor — Phase 4 creates this with an EF Core query pre-filtered by CollectionId:
///        new CmsQueryable&lt;BlogPostsEntry&gt;(
///            db.Entries.Where(e => e.CollectionId == collectionId),
///            mappings)
///      This creates the CmsQueryProvider and sets Expression to ConstantExpression(this).
///
///   2. Composed constructor — created by CmsQueryProvider.CreateQuery when HC chains operations.
///      Reuses the same provider but with a new (larger) expression tree.
///
/// The expression tree grows like:
///   Constant(CmsQueryable)                                          ← root
///   → MethodCall(Where, [^, lambda])                                ← HC filtering
///   → MethodCall(OrderBy, [^, lambda])                              ← HC sorting
///   → MethodCall(Skip, [^, 10])                                     ← HC paging
///   → MethodCall(Take, [^, 21])                                     ← HC paging (+1 for hasNextPage)
///
/// Nothing executes until HC calls ToListAsync/CountAsync, which triggers GetAsyncEnumerator
/// or Provider.Execute, causing the full expression tree to be rewritten and executed.
/// </summary>
public class CmsQueryable<T> : IQueryable<T>, IOrderedQueryable<T>, IAsyncEnumerable<T>
{
    private readonly CmsQueryProvider _provider;
    private readonly Expression _expression;
    private readonly IQueryable<Entry> _innerSource;
    private readonly CmsFieldMapping[] _mappings;

    /// <summary>
    /// Root constructor — creates a fresh CmsQueryProvider and sets the expression root.
    /// Called by Phase 4's query resolvers to create the initial queryable for a collection.
    ///
    /// innerSource should be pre-filtered by CollectionId so only entries for the target
    /// collection are queried. mappings come from CmsTypeFactory.GetFieldMappings().
    /// </summary>
    public CmsQueryable(IQueryable<Entry> innerSource, CmsFieldMapping[] mappings)
    {
        _innerSource = innerSource;
        _mappings = mappings;
        _provider = new CmsQueryProvider(innerSource, mappings, typeof(T));
        // Expression.Constant(this, typeof(IQueryable<T>)) — the typed constant is important
        // so CmsExpressionRewriter.VisitConstant can detect it by element type.
        _expression = Expression.Constant(this, typeof(IQueryable<T>));
    }

    /// <summary>
    /// Composed constructor — used when HC chains LINQ operations via CmsQueryProvider.CreateQuery.
    /// Shares the same provider (and thus the same inner source + mappings) but with the
    /// accumulated expression tree.
    /// </summary>
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

    /// <summary>
    /// Synchronous enumeration path — used by HC's CountAsync (via Task.Run(() => source.Count()))
    /// and as a fallback when IAsyncEnumerable isn't available.
    /// </summary>
    public IEnumerator<T> GetEnumerator()
    {
        return Enumerate().GetEnumerator();
    }

    /// <summary>
    /// Explicit IEnumerable.GetEnumerator implementation to satisfy the non-generic IEnumerable
    /// interface required by IQueryable. Delegates to the generic GetEnumerator.
    /// </summary>
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Primary data retrieval path for HC's paging middleware.
    ///
    /// HC's DefaultQueryableExecutable checks `source is IAsyncEnumerable<T>` and if true,
    /// iterates via `await foreach`. This is the hot path for query execution.
    ///
    /// The flow:
    ///   1. Rewrite the accumulated expression tree (dynamic type → Entry + JSONB)
    ///   2. Create an EF Core queryable from the rewritten expression
    ///   3. Iterate entries asynchronously
    ///   4. Materialize each Entry → T via CmsObjectMaterializer
    ///
    /// We wrap the async iteration in an IAsyncEnumerable method rather than directly
    /// returning an enumerator, so the caller can manage the cancellation token lifecycle.
    /// </summary>
    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        return EnumerateAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);
    }

    /// <summary>
    /// Synchronous enumeration — rewrites the expression, creates an EF Core query,
    /// and yields materialized dynamic type instances one at a time.
    ///
    /// Uses yield return to avoid buffering all results in memory before returning any.
    /// </summary>
    private IEnumerable<T> Enumerate()
    {
        var rewritten = _provider.Rewrite(_expression);
        var entryQuery = _innerSource.Provider.CreateQuery<Entry>(rewritten);

        foreach (var entry in entryQuery)
            yield return CmsObjectMaterializer.Materialize<T>(entry, _mappings);
    }

    /// <summary>
    /// Asynchronous enumeration — the primary path for HC's ToListAsync.
    ///
    /// After rewriting, we check if the resulting EF Core query implements IAsyncEnumerable&lt;Entry&gt;
    /// (which EntityQueryable does). If so, we use the async path with cancellation support.
    /// The sync fallback handles edge cases where EF Core returns a non-async queryable
    /// (shouldn't happen in normal operation but defensive).
    ///
    /// ConfigureAwait(false) avoids capturing the synchronization context since we don't
    /// need to return to a specific context — materialization is context-free.
    /// </summary>
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
            // Fallback: synchronous iteration when async isn't available.
            // This shouldn't happen with EF Core's EntityQueryable but provides safety.
            foreach (var entry in entryQuery)
                yield return CmsObjectMaterializer.Materialize<T>(entry, _mappings);
        }
    }
}
