using Scaffold.Containers;
using UnityEngine;

namespace Scaffold.Navigation.Container
{
    [CreateAssetMenu(menuName = "Scaffold/Navigation/Config")]
    public class NavigationConfig : ScriptableObject, IConfig
    {
        [SerializeField] private NavigationSettings settings;

        public NavigationSettings Settings => settings;
    }
}
