using System;
using System.Linq;
using System.Linq.Expressions;

namespace Scaffold.MVVM.Binding
{
    public static class ExpressionsUtility
    {
        public static string GetPropertyName<T>(this Expression<Func<T>> propertyLambda)
        {
            MemberExpression me = propertyLambda.Body as MemberExpression;
            if (me == null) { throw new ArgumentException("You must pass a lambda of the form: '() => Class.Property' or '() => object.Property'"); }
            return BuildPropertyPath(me);
        }

        private static string BuildPropertyPath(MemberExpression me)
        {
            string result = string.Empty;
            do
            {
                result = me.Member.Name + "." + result;
                me = me.Expression as MemberExpression;
            } while (me != null);
            return result.Remove(result.Length - 1);
        }

        public static Expression<Action<TEntity, TProperty>> CreateSetter<TEntity, TProperty>(this Expression<Func<TEntity, TProperty>> selector)
        {
            var valueParam = Expression.Parameter(typeof(TProperty));
            var body = Expression.Assign(selector.Body, valueParam);
            var singleParameter = selector.Parameters.Single();
            return Expression.Lambda<Action<TEntity, TProperty>>(body, singleParameter, valueParam);
        }

        public static Expression<Action<TProperty>> CreateSetter<TProperty>(this Expression<Func<TProperty>> selector)
        {
            var valueParam = Expression.Parameter(typeof(TProperty));
            var body = Expression.Assign(selector.Body, valueParam);
            return Expression.Lambda<Action<TProperty>>(body, valueParam);
        }
    }
}
