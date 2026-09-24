using System.Linq.Expressions;
using System.Reflection;
using Moongate.Core.Primitives;

namespace Moongate.Persistence.Internal;

internal sealed class SerialConstantExpressionVisitor : ExpressionVisitor
{
    protected override Expression VisitNew(NewExpression node)
    {
        if (node.Type != typeof(Serial))
        {
            return base.VisitNew(node);
        }

        if (node.Arguments.Count != 1 || !TryReadClosedValue(node.Arguments[0], out var value) || value is not uint serial)
        {
            throw new NotSupportedException(
                "Serial constructors in predicates require a constant or captured uint argument."
            );
        }

        return Expression.Constant(new Serial(serial));
    }

    private static bool TryReadClosedValue(Expression expression, out object? value)
    {
        if (expression is ConstantExpression constant)
        {
            value = constant.Value;

            return true;
        }

        if (expression is MemberExpression { Member: FieldInfo field } member &&
            (member.Expression is null || TryReadClosedValue(member.Expression, out _)))
        {
            object? instance = null;

            if (member.Expression is not null)
            {
                TryReadClosedValue(member.Expression, out instance);
            }

            value = field.GetValue(instance);

            return true;
        }

        value = null;

        return false;
    }
}
