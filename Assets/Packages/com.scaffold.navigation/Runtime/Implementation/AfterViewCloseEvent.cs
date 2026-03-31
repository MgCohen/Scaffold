using System;
using Scaffold.Events.Contracts;

namespace Scaffold.Navigation
{
    public record AfterViewCloseEvent(Type ViewType) : ContextEvent;
}
