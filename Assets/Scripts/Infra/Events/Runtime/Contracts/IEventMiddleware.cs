using System;

namespace Scaffold.Events
{
    public interface IEventMiddleware
    {
        void Invoke(ContextEvent evt, Action next);
    }
}
