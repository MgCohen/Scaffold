using System;
using Scaffold.Events.Contracts;

namespace Scaffold.Navigation
{
    public record AfterViewOpenEvent(Type ViewType) : ContextEvent;
}
