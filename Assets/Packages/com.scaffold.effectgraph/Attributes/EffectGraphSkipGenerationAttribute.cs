using System;

namespace Scaffold.EffectGraph
{
    /// <summary>
    /// When applied to an assembly, suppresses EFG001 for that assembly (assembly does not use effect graphs).
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
    public sealed class EffectGraphSkipGenerationAttribute : Attribute
    {
    }
}
