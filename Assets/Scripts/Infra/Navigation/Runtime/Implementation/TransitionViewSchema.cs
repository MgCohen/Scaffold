using UnityEngine;
using Scaffold.Types;
using Scaffold.Events.Contracts;
using Scaffold.Events;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System;
using Scaffold.Navigation.Contracts;
namespace Scaffold.Navigation
{
    public class TransitionViewSchema : ViewSchema
    {
        public TransitionHandler Handler => handler;
        public TransitionDirection Direction => direction;

        [TypeReferenceFilter(typeof(IView))]
        [SerializeField] private List<TypeReference> viewTypes = new();
        [SerializeField] private ViewFilter filter = ViewFilter.Any;
        [SerializeField] private TransitionDirection direction;
        [SerializeField] private TransitionHandler handler;

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
            if (filter is ViewFilter.SpecificViews && !contains) return false;
            return !(filter is ViewFilter.Any && contains);
        }

        private bool CheckContains(NavigationPoint targetPoint)
        {
            return viewTypes.Any(vt => vt.Type == targetPoint?.Config.ViewType);
        }
    }
}


