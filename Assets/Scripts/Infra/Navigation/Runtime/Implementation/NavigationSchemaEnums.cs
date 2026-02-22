using System;

namespace Scaffold.Navigation
{
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
