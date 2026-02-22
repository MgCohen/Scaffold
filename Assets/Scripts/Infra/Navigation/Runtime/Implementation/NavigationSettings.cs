
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

            var isController = typeof(IViewController).IsAssignableFrom(type);
            config = screens.FirstOrDefault(s => isController ? s.ControllerType.IsAssignableFrom(type) : s.ViewType.IsAssignableFrom(type));
            if (!config)
            {
                throw new Exception($"No view config found for {type.Name}");
            }
            cachedConfigs[type] = config;
            return config;
        }

        //public bool TryGetOverlayConfig(Type type, out OverlayConfig config)
        //{
        //    config = overlays.FirstOrDefault(s => s.Type.IsAssignableFrom(type));
        //    return config != null;
        //}
    }
}