using UnityEngine;
using Scaffold.Types;
using Scaffold.Events.Contracts;
using Scaffold.Events;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System;
using Scaffold.Navigation.Contracts;
using Scaffold.Navigation.Utility;
namespace Scaffold.Navigation
{
    public class AnimationViewSchema : ViewSchema
    {
        public AnimationHandler Handler => handler;
        public AnimationType Direction => direction;
        public string AnimationName => animationName;

        [TypeReferenceFilter(typeof(IView))]
        [SerializeField] private List<TypeReference> viewTypes = new();
        [SerializeField] private ViewFilter filter = ViewFilter.Any;
        [SerializeField] private AnimationType direction;
        [SerializeField] private AnimationHandler handler;
        [SerializeField] private string animationName;

        public bool IsValidAnimation(NavigationPoint from, NavigationPoint to, AnimationType direction)
        {
            var targetPoint = direction is AnimationType.Opening ? from : to;
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
            return viewTypes.Any(vt => vt.Type == targetPoint?.Config.ViewType || (targetPoint == null && vt.Type == typeof(NoView)));
        }
    }
}


