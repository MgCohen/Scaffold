using System;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Scaffold.Core.Variables
{
    public enum VariableSource
    {
        Constant,
        Reference
    }

    [Serializable]
    public abstract class VariableReference<T1, T2> where T2 : ScriptableObjectReference<T1>
    {
        [SerializeField, HideInInspector]
        private VariableSource source = VariableSource.Reference;

        [ShowInInspector, PropertyOrder(-1)]
        [EnumToggleButtons, HideLabel]
        private VariableSource Source
        {
            get => source;
            set => source = value;
        }

        [OdinSerialize, ShowIf("source", VariableSource.Constant), HideLabel]
        protected T1 data;

        [SerializeField, InlineEditor]
        [ShowIf("source", VariableSource.Reference), HideLabel]
        protected T2 reference;

        public T1 Value
        {
            get
            {
                if (source == VariableSource.Constant) return data;
                return reference != null ? reference.Data : default;
            }
        }

        public void SetData(T1 value)
        {
            source = VariableSource.Constant;
            data = value;
        }
        
        public void SetReference(T2 value)
        {
            source = VariableSource.Reference;
            reference = value;
        }

        public static implicit operator T1(VariableReference<T1, T2> reference)
        {
            return reference != null ? reference.Value : default;
        }
    }
}
