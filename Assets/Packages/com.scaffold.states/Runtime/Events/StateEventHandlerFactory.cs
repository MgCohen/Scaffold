#nullable enable

namespace Scaffold.States
{
    public static class StateEventHandlerFactory
    {
        public static IStateEventHandler CreateDefault()
        {
            return new StateEventHandler();
        }
    }
}
