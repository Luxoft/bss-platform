using System.Linq.Expressions;
using System.Reflection;

using NHibernate.Linq;

namespace Bss.Platform.NHibernate.UnitTesting.Queryable;

internal class ExpressionTreeModifier : ExpressionVisitor
{
    private static readonly HashSet<MethodInfo> VisitedMethods =
        new[]
            {
                nameof(EagerFetchingExtensionMethods.Fetch),
                nameof(EagerFetchingExtensionMethods.FetchMany),
                nameof(EagerFetchingExtensionMethods.ThenFetch),
                nameof(EagerFetchingExtensionMethods.ThenFetchMany)
            }
            .Select(x => typeof(EagerFetchingExtensionMethods).GetMethod(x)!)
            .ToHashSet();

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        if (!IsFetchMethod(node.Method))
        {
            return base.VisitMethodCall(node);
        }

        var fetchInput = AvoidFetch(node);
        return fetchInput.NodeType switch
               {
                   ExpressionType.Constant => this.VisitConstant((ConstantExpression)fetchInput),
                   _ => fetchInput
               };
    }

    private static Expression AvoidFetch(MethodCallExpression node) =>
        IsFetchMethod(node.Method) ? AvoidFetch((node.Arguments[0] as MethodCallExpression)!) : node;

    private static bool IsFetchMethod(MethodInfo info) =>
        info.IsGenericMethod && VisitedMethods.Contains(info.GetGenericMethodDefinition());
}
