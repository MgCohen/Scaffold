using Scaffold.States;
using System.Collections;
using System.Collections.Generic;

namespace Scaffold.Game.Stack
{

    public interface IPriorityHandler
    {
        void SetPriority(IPlayer player);
        void PassPriority();
        bool CheckForPlayerPriority();
        IPlayer GetPriorityPlayer();
    }

    public class PriorityHandler : IPriorityHandler
    {
        public PriorityHandler(ITurnHandler turn, IActionHandler actions, Store store)
        {
            this.actions = actions;
            this.turn = turn;
            this.store = store;
        }

        private Store store;
        private IActionHandler actions;
        private ITurnHandler turn;

        public void SetPriority(IPlayer player)
        {
            store.Execute(new ChangePriorityPlayer(player));
        }

        public IPlayer GetPriorityPlayer()
        {
            PriorityState state = store.Get<PriorityState>();
            return state.Priority;
        }

        public bool CheckForPlayerPriority()
        {
            PriorityState state = store.Get<PriorityState>();
            return state.Priority != null;
        }

        public void PassPriority()
        {
            IPlayerAction previousAction = actions.CheckPreviousAction();
            //if (previousAction is PassAction)
            //{
            //    store.Execute(new ChangePriorityPlayer(null));
            //}
            //else
            //{
            //    MovePriority();
            //}
        }

        private void MovePriority()
        {
            var priority = GetPriorityPlayer();
            IPlayer next = turn.GetNextPlayer(priority);
            SetPriority(priority);
        }
    }

    public record PriorityState(IPlayer Active, IPlayer Priority, IEnumerable<IPlayer> Order) : State;

    public class ChangePriorityPlayer : Mutator<PriorityState>
    {
        public ChangePriorityPlayer(IPlayer player)
        {
            Player = player;
        }

        public IPlayer Player { get; }

        public override PriorityState Change(PriorityState state)
        {
            return state with { Priority = Player };
        }
    }

    public class ChangeActivePlayer : Mutator<PriorityState>
    {
        public ChangeActivePlayer(IPlayer player)
        {
            Player = player;
        }

        public IPlayer Player { get; }

        public override PriorityState Change(PriorityState state)
        {
            return state with { Active = Player };
        }
    }
}
