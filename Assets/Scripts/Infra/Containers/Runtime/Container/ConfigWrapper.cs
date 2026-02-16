using UnityEngine;

namespace Scaffold.Containers
{
    /// <summary>
    /// Wraps a UnityEngine.Object-backed IConfig for storage in ContainerConfig.
    /// </summary>
    public class ConfigWrapper<T> : IConfigWrapper where T : UnityEngine.Object, IConfig
    {
        [SerializeField] private T config;

        public T Config => config;

        IConfig IConfigWrapper.Config => config;

        public ConfigWrapper(T config)
        {
            this.config = config;
        }
    }
}
