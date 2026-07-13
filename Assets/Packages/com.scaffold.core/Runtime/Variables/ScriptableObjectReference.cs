using System;
using Sirenix.OdinInspector;

namespace Scaffold.Core.Variables
{
    [Serializable]
    public abstract class ScriptableObjectReference : SerializedScriptableObject
    {
        public virtual object obj => null;
    }
    
    [Serializable]
    public abstract class ScriptableObjectReference<T> : ScriptableObjectReference
    {
        public override object obj => data;
        
        [OdinSerialize, HideLabel]
        protected T data;
        
        public virtual T Data
        {
            get => data;
            set => data = value;
        }
    }
}
