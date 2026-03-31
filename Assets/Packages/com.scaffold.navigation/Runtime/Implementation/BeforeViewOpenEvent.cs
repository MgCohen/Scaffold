using System;
using Scaffold.Events.Contracts;

namespace Scaffold.Navigation
{
    public record BeforeViewOpenEvent(Type ViewType) : ContextEvent;
}
