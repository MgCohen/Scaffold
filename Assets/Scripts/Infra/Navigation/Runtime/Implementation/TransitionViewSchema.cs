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
            return IsValidViewFilter(targetPoint);
        }

        private bool IsValidViewFilter(NavigationPoint targetPoint)
        {
            bool contains = CheckContains(targetPoint);
            if (IsBlockedBySpecificFilter(contains))
            {
                return false;
            }
            return !IsBlockedByAnyFilter(contains);
        }

        private bool CheckContains(NavigationPoint targetPoint)
        {
            return viewTypes.Any(vt => vt.Type == targetPoint?.Config.ViewType);
        }

        private bool IsBlockedBySpecificFilter(bool contains)
        {
            return filter is ViewFilter.SpecificViews && !contains;
        }

        private bool IsBlockedByAnyFilter(bool contains)
        {
            return filter is ViewFilter.Any && contains;
        }
    }
}
