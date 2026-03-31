using System;
namespace Scaffold.MVVM.Binding
{
    internal static class BindedPropertyUtility
    {
        public static IBindedProperty<TSource, TTarget> WithConverter<TSource, TTarget>(this IBindedProperty<TSource, TTarget> property, Func<TSource, TTarget> converter)
        {
            GenericConverter<TSource, TTarget> genericConverter = new GenericConverter<TSource, TTarget>(converter);
            return property.WithConverter(genericConverter);
        }
    }
}






