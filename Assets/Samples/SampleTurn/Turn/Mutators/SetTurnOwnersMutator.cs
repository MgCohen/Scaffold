using System.Collections.Generic;
using Scaffold.States;

namespace Sample.Turn.Mutators
{
    /// <summary>
    /// Replaces the current turn owners in TurnOrderState.
    /// </summary>
    public class SetTurnOwnersMutator : Mutator<TurnOrderState>
    {
        private readonly IReadOnlyList<MatchPlayer> _turnOwners;

        public SetTurnOwnersMutator(IReadOnlyList<MatchPlayer> turnOwners)
        {
            _turnOwners = turnOwners;
        }

        public override TurnOrderState Change(TurnOrderState state)
        {
            return state with { TurnOwners = _turnOwners };
        }
    }
}
