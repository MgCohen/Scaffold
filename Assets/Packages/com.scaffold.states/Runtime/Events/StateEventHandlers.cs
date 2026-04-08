#nullable enable

namespace Scaffold.States
{
    public static class StateEventHandlers
    {
        public static IStateEventHandler CreateDefault()
        {
            return new StateEventHandler();
        }
    }
}
