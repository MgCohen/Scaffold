using Unity.GraphToolkit.Editor;

namespace Scaffold.GraphFlow.Editor
{
    /// <summary>Builds a typed <see cref="VariableDefault"/> from a GT <see cref="IVariable"/>.
    /// Dispatches on <c>variable.DataType</c> to call the typed
    /// <c>TryGetDefaultValue&lt;T&gt;</c> overload — no boxing, no reflection.</summary>
    static class EditorVariableDefaults
    {
        public static VariableDefault? CreateFor(IVariable variable)
        {
            if (variable == null) return null;
            var t = variable.dataType;
            if (t == null) return null;

            if (t == typeof(int))    { variable.TryGetDefaultValue(out int v);    return new IntDefault    { value = v }; }
            if (t == typeof(float))  { variable.TryGetDefaultValue(out float v);  return new FloatDefault  { value = v }; }
            if (t == typeof(bool))   { variable.TryGetDefaultValue(out bool v);   return new BoolDefault   { value = v }; }
            if (t == typeof(string)) { variable.TryGetDefaultValue(out string v); return new StringDefault { value = v ?? string.Empty }; }
            if (typeof(UnityEngine.Object).IsAssignableFrom(t))
            {
                variable.TryGetDefaultValue(out UnityEngine.Object v);
                return new ObjectDefault { value = v };
            }
            return null;
        }
    }
}
