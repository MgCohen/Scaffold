using System;
using UnityEngine;
using UnityEngine.Scripting;

namespace Scaffold.Entities
{
    [Serializable]
    [Preserve]
    public abstract class VariableModifier
    {
        public int Order => order;

        [SerializeField]
        private int order;
    }

    [Serializable]
    public abstract class VariableModifier<T> : VariableModifier
    {
        public abstract T Apply(T current);
    }
}
