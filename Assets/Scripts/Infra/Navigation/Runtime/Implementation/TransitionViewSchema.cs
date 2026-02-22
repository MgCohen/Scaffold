using System.Collections.Generic;
using System.Linq;
using Scaffold.Types;
using UnityEngine;

namespace Scaffold.Navigation
{
    public class TransitionViewSchema : ViewSchema
    {
        [TypeReferenceFilter(typeof(IView))]
        [SerializeField] private List<TypeReference> viewTypes = new();
        [SerializeField] private ViewFilter filter = ViewFilter.Any;
        [SerializeField] private TransitionDirection direction;
        [SerializeField] private TransitionHandler handler;

        public TransitionHandler Handler => handler;
        public TransitionDirection Direction => direction;

        public bool IsValidTransition(NavigationPoint from, NavigationPoint to, TransitionDirection direction)
        {
            var targetPoint = direction == TransitionDirection.FromThisView ? to : from;
            if (!Direction.HasFlag(direction))
            {
                return false;
            }

            bool contains = viewTypes.Any(vt => vt.Type == targetPoint?.Config.ViewType);
            if (filter is ViewFilter.SpecificViews && !contains)
            {
                return false;
            }

            if (filter is ViewFilter.Any && contains)
            {
                return false;
            }

            return true;
        }
    }
}
