
using JetBrains.Annotations;
using Newtonsoft.Json;
using System;
using UnityEngine;

namespace Scaffold.Types
{
    [Serializable]
    public class TypeReference
    {
        public TypeReference()
        {

        }

        public TypeReference(Type type)
        {
            Set(type);
        }

        private Type type;
        [SerializeField] private string serializedType;

        public Type Type
        {
            get
            {
                try
                {
                    if (type == null && !string.IsNullOrWhiteSpace(serializedType))
                    {
                        var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };
                        type = JsonConvert.DeserializeObject<Type>(serializedType, settings);
                    }
                }
                catch
                {
                    Debug.LogWarning($"Failed to deserialize type value from:\n{serializedType}");
                    type = null;
                }
                return type;
            }
        }

        public void Set<T>()
        {
            Set(typeof(T));
        }

        public void Set(Type type)
        {
            var hasType = type != null;
            if (hasType)
            {
                this.type = type;
                var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };
                serializedType = JsonConvert.SerializeObject(type, settings);
            }
        }

        public static implicit operator TypeReference(Type type) => new(type);
    }
}
