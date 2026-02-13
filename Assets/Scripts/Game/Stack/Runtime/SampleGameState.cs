
using Scaffold.States;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using UnityEngine;
using UnityEngine.Assertions;

namespace Scaffold.States
{
    public class Main
    {
        Store store;
        public void Build()
        {
            StoreBuilder factory = new StoreBuilder();

            Player player = new Player();

            var store = factory.BuildSlice(player, new PlayerState(null, null))
                               .BuildSlice(player, new PlayerZoneState(null))
                               .WithBuilder(new GenericStateBuilder<Player, PlayerState>(null), player)
                               .WithBuilder((p) => new PlayerState(null, null), player)
                               .Build();

            //ChangePlayerVariable changePlayer = new ChangePlayerVariable("a", 2);
            store.SaveSnapshot();

            store.Subscribe<PlayerState>(player, (r, p) => Debug.Log("State Changed"));
            //store.Execute(player, changePlayer);
            //store.Execute(new PongPinger());
        }
    }
    #region Sample State

    public record PlayerState(string PlayerId, Dictionary<string, int> variables) : State;

    public record PlayerZoneState(IDictionary<IReference, Zone> cards) : State; 

    public class Player : IReference
    {
        public string playerId;
        public string playerName;
        public Dictionary<string, object> Blackboard;
        public PlayerCards Cards;
    }

    public class PlayerCards
    {
        //card position is a state thing, all we care is that cards are correctly initialized
        public List<Card> allCards;
    }

    public class Card: IReference
    {

    }

    public class Zone
    {

    }
    #endregion

    #region Mutators
    //public class ChangePlayerVariable : Mutator<PlayerState>
    //{
    //    private string key;
    //    private int amount;

    //    public ChangePlayerVariable(string key, int amount)
    //    {
    //        this.key = key;
    //        this.amount = amount;
    //    }

    //    public override PlayerState Change(PlayerState state)
    //    {
    //        Dictionary<string, int> variables = new(state.variables);
    //        variables[key] = amount;
    //        return state with { variables = variables};
    //    }
    //}

    #endregion
}
