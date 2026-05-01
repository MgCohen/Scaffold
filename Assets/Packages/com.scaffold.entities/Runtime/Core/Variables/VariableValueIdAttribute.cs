using System;
using UnityEngine.Scripting;

namespace Scaffold.Entities
{
    [Preserve]
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class VariableValueIdAttribute : Attribute
    {
        public VariableValueIdAttribute(string id)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
        }

        public string Id { get; }
    }
}
