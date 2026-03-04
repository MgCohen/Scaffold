using Scaffold.States;
using System.Collections.Generic;
using System.Linq;

namespace Scaffold.Game.Stack
{


    public interface IStackHandler
    {
        void Push(IStackable stackable);
        IStackable GetNext();
        void Dispose();
        bool IsStackEmpty();
    }

    public class StackHandler : IStackHandler
    {
        public StackHandler(Store store)
        {
            this.store = store;
        }

        private Store store;
        private Queue<IStackable> stash = new Queue<IStackable>();
        private IStackable active;

        public void Push(IStackable stackable)
        {
            if (active == null)
            {
                Add(stackable);
            }
            else
            {
                Stash(stackable);
            }
        }

        private void Stash(IStackable stackable)
        {
            stash.Enqueue(stackable);
        }

        private void Add(IStackable stackable)
        {
            store.Execute(new AddToStack(stackable));
        }

        public IStackable GetNext()
        {
            StackState stack = store.Get<StackState>();
            stack.Stack.TryPeek(out var state);
            return state;
        }

        public bool IsStackEmpty()
        {
            StackState stack = store.Get<StackState>();
            return stack.Stack.Count() <= 0 && stash.Count() <= 0;
        }

        public void Dispose()
        {
            PushStashIntoStack();
            active = null;
        }

        private void PushStashIntoStack()
        {
            while (stash.TryDequeue(out var stackable))
            {
                Add(stackable);
            }
        }
    }

    public record StackState(Stack<IStackable> Stack) : State;

    public class AddToStack : Mutator<StackState>
    {
        public IStackable Stackable { get; }

        public AddToStack(IStackable stackable)
        {
            Stackable = stackable;
        }

        public override StackState Change(StackState state)
        {
            Stack<IStackable> stack = new Stack<IStackable>(state.Stack.Append(Stackable));
            return state with { Stack = stack };
        }
    }

}
