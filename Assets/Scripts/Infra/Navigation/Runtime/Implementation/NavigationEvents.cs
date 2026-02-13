using Scaffold.Events;
using System;

namespace Scaffold.Navigation
{
    public record BeforeViewCloseEvent : ContextEvent
    {
        public BeforeViewCloseEvent(Type viewType)
        {
            ViewType = viewType;
        }

        public Type ViewType;
    }

    public record AfterViewCloseEvent : ContextEvent
    {
        public AfterViewCloseEvent(Type viewType)
        {
            ViewType = viewType;
        }

        public Type ViewType;
    }

    public record BeforeViewOpenEvent : ContextEvent
    {
        public BeforeViewOpenEvent(Type viewType)
        {
            ViewType = viewType;
        }

        public Type ViewType;
    }

    public record AfterViewOpenEvent : ContextEvent
    {
        public AfterViewOpenEvent(Type viewType)
        {
            ViewType = viewType;
        }

        public Type ViewType;
    }
}
