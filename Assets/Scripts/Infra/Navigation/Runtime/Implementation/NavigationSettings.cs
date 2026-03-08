
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scaffold.Navigation
{
    [CreateAssetMenu(menuName = "Scaffold/Core/Settings/Navigation")]
    public class NavigationSettings : ScriptableObject
    {
        [SerializeField] private List<ViewConfig> screens = new List<ViewConfig>();

        private Dictionary<Type, ViewConfig> cachedConfigs = new Dictionary<Type, ViewConfig>();

        public ViewConfig GetViewConfig(Type type)
        {
            if (cachedConfigs.TryGetValue(type, out ViewConfig config))
            {
                return config;
            }
            return GetAndCacheConfig(type);
        }

        private ViewConfig GetAndCacheConfig(Type type)
        {
            var isController = typeof(IViewController).IsAssignableFrom(type);
            var config = FindViewConfig(type, isController);
            cachedConfigs[type] = config;
            return config;
        }

        private ViewConfig FindViewConfig(Type type, bool isController)
        {
            var config = screens.FirstOrDefault(s => isController ? s.ControllerType.IsAssignableFrom(type) : s.ViewType.IsAssignableFrom(type));
            if (!config)
            {
                throw new Exception($"No view config found for {type.Name}");
            }
            return config;
        }
    }
}
