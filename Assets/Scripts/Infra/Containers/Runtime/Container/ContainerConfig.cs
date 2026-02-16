using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scaffold.Containers
{
    [CreateAssetMenu(menuName = "Scaffold/Container/Config")]
    public class ContainerConfig : ScriptableObject
    {
        [SerializeReference]
        [SerializeField]
        private List<IConfig> configs = new List<IConfig>();

        private Dictionary<Type, IConfig> cache = new Dictionary<Type, IConfig>();

        public void Add(IConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));
            configs.Add(config);
        }

        public T Fetch<T>() where T : class, IConfig
        {
            var type = typeof(T);
            if (TryGetFromCache(type, out T cached))
                return cached;
            if (TryGetFromList(type, out T fromList))
                return fromList;
            throw new InvalidOperationException($"No config of type {type} found.");
        }

        private bool TryGetFromCache<T>(Type type, out T config) where T : class, IConfig
        {
            if (cache.TryGetValue(type, out IConfig cached))
            {
                config = (T)cached;
                return true;
            }
            config = null;
            return false;
        }

        private bool TryGetFromList<T>(Type type, out T config) where T : class, IConfig
        {
            var result = configs.Select(Unwrap).OfType<T>().FirstOrDefault();
            if (result == null)
            {
                config = null;
                return false;
            }
            cache[type] = result;
            config = result;
            return true;
        }

        private static IConfig Unwrap(IConfig c) => c is IConfigWrapper w ? w.Config : c;
    }
}
