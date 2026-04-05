using System.Collections.Generic;

namespace Scaffold.States
{
    public class StoreBuilder
    {
        private IStateEventHandler eventHandler;
        private List<Slice> entries = new List<Slice>();

        public void AddEventHandler(IStateEventHandler eventHandler)
        {
            this.eventHandler = eventHandler;
        }

        public void AddState(State state)
        {
            AddState(null, state);
        }

        public void AddState(IReference reference, State state)
        {
            entries.Add(Slice.Create(reference, state));
        }

        public Store Build()
        {
            IStateEventHandler stateHandler = eventHandler ?? GetDefaultStateEventHandler();
            return new Store(stateHandler, entries.ToArray());
        }

        private IStateEventHandler GetDefaultStateEventHandler()
        {
            return new StateEventHandler();
        }
    }
}
