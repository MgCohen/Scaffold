using Scaffold.MVVM;
using System.Collections.Generic;
using System.Linq;

namespace Scaffold.Navigation
{
    public class HeroHandler
    {
        private List<IHeroHandler> heroTransitions = new List<IHeroHandler>();

        private void CheckForHeroTransitoins(MVVM.IView from, MVVM.IView to)
        {
            var currentMarkers = from.gameObject.GetComponentsInChildren<IHeroHandler>().ToList();
            var newMarkers = to.gameObject.GetComponentsInChildren<IHeroHandler>().ToList();
            foreach (var current in currentMarkers)
            {
                var mark = newMarkers.Find(m => m.HeroId == current.HeroId);
                if (mark != null)
                {
                    newMarkers.Remove(mark);
                    current.SetAnchor();
                    current.transform.SetParent(from.gameObject.transform.parent, true);
                    heroTransitions.Add(current);
                }
            }
        }


        private void DoHeroTransitions(MVVM.IView to)
        {
            var newMarkers = to.gameObject.GetComponentsInChildren<IHeroHandler>().ToList();
            foreach (var current in heroTransitions)
            {
                var mark = newMarkers.Find(m => m.HeroId == current.HeroId);
                if (mark != null)
                {
                    newMarkers.Remove(mark);
                    current.transform.SetParent(mark.transform.parent, true);
                    mark.DoHeroTransition(current);
                    current.ResetAnchor();
                }
            }
            heroTransitions.Clear();
        }
    }
}
