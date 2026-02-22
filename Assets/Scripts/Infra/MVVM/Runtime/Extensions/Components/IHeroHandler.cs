using UnityEngine;

namespace Scaffold.Navigation
{
    public interface IHeroHandler
    {
        public Transform transform { get; }
        public string HeroId { get; }
        public void SetAnchor();
        public void ResetAnchor();
        public void DoHeroTransition(IHeroHandler from);
    }
}