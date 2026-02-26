using Scaffold.Events;
using System;

namespace Scaffold.Navigation
{
    public record BeforeViewCloseEvent(Type ViewType) : ContextEvent;

    public record AfterViewCloseEvent(Type ViewType) : ContextEvent;

    public record BeforeViewOpenEvent(Type ViewType) : ContextEvent;

    public record AfterViewOpenEvent(Type ViewType) : ContextEvent;
}
