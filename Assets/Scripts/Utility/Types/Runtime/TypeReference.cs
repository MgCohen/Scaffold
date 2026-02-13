
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
                        type = JsonConvert.DeserializeObject<Type>(serializedType, new JsonSerializerSettings()
                        {
                            TypeNameHandling = TypeNameHandling.All,
                        });
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
            if (type == null)
            {
                return;
            }

            this.type = type;
            serializedType = JsonConvert.SerializeObject(type, new JsonSerializerSettings()
            {
                TypeNameHandling = TypeNameHandling.All,
            });
        }

        public static implicit operator TypeReference(Type type) => new(type);
    }
}
