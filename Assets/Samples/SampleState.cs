using Scaffold.States;
using System.Collections.Generic;

namespace Sample.States
{
    #region Player State
    public record PlayerState : State
    {
        public Dictionary<string, int> Variables { get; init; }
    }

    public record PlayerCardsState : State
    {
        public Dictionary<Card, Zone> CardLookUp { get; init; }
    }

    public record CardState: State;
    #endregion


    public class PlayerVariablesMutator : Mutator<PlayerState>
    {
        public override PlayerState Change(PlayerState state)
        {
            Dictionary<string, int> variables = new(state.Variables);
            variables.Add("Mana", 54);
            return state with { Variables = variables };
        }
    }
}
