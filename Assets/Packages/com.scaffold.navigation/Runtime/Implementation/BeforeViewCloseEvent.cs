using System;
using Scaffold.Events.Contracts;

namespace Scaffold.Navigation
{
    public record BeforeViewCloseEvent(Type ViewType) : ContextEvent;
}
