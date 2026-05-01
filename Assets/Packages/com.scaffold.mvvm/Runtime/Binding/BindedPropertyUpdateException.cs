using System;

namespace Scaffold.MVVM.Binding
{
    internal sealed class BindedPropertyUpdateException<TSource, TTarget> : Exception
    {
        public BindedPropertyUpdateException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public BindedPropertyUpdateException(TSource value, Action<TTarget> setter, Exception innerException) : base(BuildMessage(value, setter), innerException)
        {
        }

        private static string BuildMessage(TSource value, Action<TTarget> setter)
        {
            string sourceType = typeof(TSource).FullName;
            string targetType = typeof(TTarget).FullName;
            string runtimeType = ToRuntimeTypeName(value);
            string valueText = ToSafeText(value);
            string setterName = ToSetterName(setter);
            return $"BindedProperty update failed. SourceType={sourceType}, TargetType={targetType}, ValueRuntimeType={runtimeType}, Value={valueText}, Setter={setterName}.";
        }

        private static string ToRuntimeTypeName(TSource value)
        {
            return value == null ? "null" : value.GetType().FullName;
        }

        private static string ToSetterName(Action<TTarget> setter)
        {
            if (setter?.Method == null)
            {
                return "null";
            }
            return $"{setter.Method.DeclaringType?.FullName}.{setter.Method.Name}";
        }

        private static string ToSafeText(object value)
        {
            if (value == null)
            {
                return "null";
            }
            try { return value.ToString(); }
            catch { return "<ToString failed>"; }
        }
    }
}






