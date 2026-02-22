using System.Collections.Generic;
using System.Linq;
using Scaffold.Types;
using UnityEngine;

namespace Scaffold.Navigation
{
    public class AnimationViewSchema : ViewSchema
    {
        [TypeReferenceFilter(typeof(IView))]
        [SerializeField] private List<TypeReference> viewTypes = new();
        [SerializeField] private ViewFilter filter = ViewFilter.Any;
        [SerializeField] private AnimationType direction;
        [SerializeField] private AnimationHandler handler;
        [SerializeField] private string animationName;

        public AnimationHandler Handler => handler;
        public AnimationType Direction => direction;
        public string AnimationName => animationName;

        public bool IsValidAnimation(NavigationPoint from, NavigationPoint to, AnimationType direction)
        {
            var targetPoint = direction is AnimationType.Opening ? from : to;
            if (!Direction.HasFlag(direction))
            {
                return false;
            }

            bool contains = viewTypes.Any(vt => vt.Type == targetPoint?.Config.ViewType || (targetPoint == null && vt.Type == typeof(NoView)));
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
