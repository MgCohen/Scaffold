using System;
using System.Collections.Generic;
using System.Linq;
using Scaffold.Schemas;
using Scaffold.Types;
using UnityEngine;

namespace Scaffold.Navigation
{
    public abstract class ViewSchema : Schema
    {

    }

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

    public enum ViewFilter
    {
        Any = 0,
        SpecificViews = 1,
    }

    [Flags]
    public enum AnimationType
    {
        Opening = 1 << 1,
        Closing = 1 << 2,
        Hiding = 1 << 3,
        Focusing = 1 << 4
    }

    public enum AnimationHandler
    {
        Template = 0,
        Code = 1,
        Animator = 2,
        Default = 3,
        Custom = 4,
    }

    [Flags]
    public enum TransitionDirection
    {
        FromThisView = 1 << 1,
        ToThisView = 1 << 2,
    }

    public enum TransitionHandler
    {
        Template = 0,
        Code = 1,
        Default = 2,
    }
}
