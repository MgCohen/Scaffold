using JetBrains.Annotations;
using Newtonsoft.Json;
using System;
using UnityEngine;

namespace Scaffold.Types
{
    [Serializable]
    public class TypeReference : ISerializationCallbackReceiver
    {
        public TypeReference()
        {

        }

        public TypeReference(Type type)
        {
            if (type is null)
            {
                throw new ArgumentNullException(nameof(type));
            }
            Set(type);
        }

        public Type Type => type;

        [SerializeField] private Type type;
        [SerializeField] private string serializedType;

        public void Set<T>()
        {
            Set(typeof(T));
        }

        public void Set(Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            this.type = type;
            var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };
            serializedType = JsonConvert.SerializeObject(type, settings);
        }

        public void OnBeforeSerialize()
        {
            if (type == null)
            {
                return;
            }

            var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };
            serializedType = JsonConvert.SerializeObject(type, settings);
        }

        public void OnAfterDeserialize()
        {
            if (string.IsNullOrWhiteSpace(serializedType))
            {
                type = null;
                return;
            }
            try { var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All }; type = JsonConvert.DeserializeObject<Type>(serializedType, settings); }
            catch { Debug.LogWarning($"Failed to deserialize type value from:\n{serializedType}"); type = null; }
        }

        public static implicit operator TypeReference(Type type) => new(type);
    }
}

