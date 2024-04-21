using System.Linq.Expressions;

using NHibernate;
using NHibernate.Linq;
using NHibernate.Type;

namespace Bss.Platform.NHibernate.UnitTesting.Queryable;

internal class TestNhQueryProvider<TDomainObject>(IQueryable<TDomainObject> source) : INhQueryProvider
{
    public IQueryable CreateQuery(Expression expression) => throw new NotImplementedException();

    public IQueryable<TElement> CreateQuery<TElement>(Expression expression) =>
        new TestQueryable<TElement>(source.Provider.CreateQuery<TElement>(expression));

    public object Execute(Expression expression) => this.ExecuteInMemoryQuery(expression);

    public TResult Execute<TResult>(Expression expression) => this.ExecuteInMemoryQuery<TResult>(expression);

    public Task<TResult> ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken) =>
        Task.FromResult(this.Execute<TResult>(expression));

    public int ExecuteDml<T1>(QueryMode queryMode, Expression expression) => throw new NotImplementedException();

    public Task<int> ExecuteDmlAsync<T1>(QueryMode queryMode, Expression expression, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public IFutureEnumerable<TResult> ExecuteFuture<TResult>(Expression expression) => throw new NotImplementedException();

    public IFutureValue<TResult> ExecuteFutureValue<TResult>(Expression expression) => throw new NotImplementedException();

    public void SetResultTransformerAndAdditionalCriteria(
        IQuery query,
        NhLinqExpression nhExpression,
        IDictionary<string, Tuple<object, IType>> parameters) =>
        throw new NotImplementedException();

    private object ExecuteInMemoryQuery(Expression expression) =>
        source.Provider.Execute(new ExpressionTreeModifier().Visit(expression)) ?? throw new Exception();

    private TResult ExecuteInMemoryQuery<TResult>(Expression expression) =>
        source.Provider.Execute<TResult>(new ExpressionTreeModifier().Visit(expression));
}
