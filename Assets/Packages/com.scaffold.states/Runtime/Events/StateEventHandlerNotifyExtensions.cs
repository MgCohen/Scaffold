#nullable enable

namespace Scaffold.States
{
    public static class StateEventHandlerNotifyExtensions
    {
        public static void Notify(this IStateEventHandler handler, Reference reference, BaseState state)
        {
            handler.Notify(reference, state, StateChangeEvent.Updated);
        }
    }
}
