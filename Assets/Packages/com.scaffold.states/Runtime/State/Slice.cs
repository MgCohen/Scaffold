namespace Scaffold.States
{
    public class Slice
    {
        private Slice()
        {

        }

        private Slice(IReference reference, State state)
        {
            this.Reference = reference;
            this.State = state;
        }

        public IReference Reference { get; private set; }
        public State State { get; private set; }

        public void Set(State state)
        {
            this.State = state;
        }

        public static Slice Create(IReference reference, State state)
        {
            reference ??= States.Reference.Null;
            return new Slice(reference, state);
        }
    }
}
