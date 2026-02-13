using Scaffold.States;
using System;
using System.Collections.Generic;

public class TurnHandler : ITurnHandler
{
    //private List<IPlayer> turnOrder;
    //private IPlayer activePlayer;
    private Store store;

    public TurnHandler(Store store)
    {
        this.store = store;
    }

    public void SetActivePlayer(IPlayer player)
    {
        //activePlayer = player;
    }

    public IPlayer GetActivePlayer()
    {
        //return activePlayer;
        return default;
    }

    public IPlayer GetNextPlayer(IPlayer player)
    {
        //int index = turnOrder.IndexOf(player);
        //index = (index + 1) % turnOrder.Count;
        //return turnOrder[index];
        return default;
    }

    public void Temporary_PassTurn()
    {
        store.Execute(new ChangeTurn());
    }
}

public record TurnState : State
{
    public int CurrentTurn;

    //public Player ActivePlayer;
    public IReference ActivePlayer;
    //public Player PriorityPlayer;
    public IReference PriorityPlayer;

    //public TurnPhase CurrentPhase;
    public IReference CurrentPhase;
    //public TurnStep CurrentStep;
    public IReference CurrentStep;
}

public class ChangeTurn : Mutator<TurnState>
{
    public override TurnState Change(TurnState state)
    {
        return state with { CurrentTurn = state.CurrentTurn + 1 };
    }
}