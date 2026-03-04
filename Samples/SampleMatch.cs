using Scaffold.States;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sample.States
{
    #region Player Definition
    public class Player: IReference
    {
        public string playerId;
        public ulong clientId;
        
        public object loadoutData;

        public Dictionary<string, int> Variables = new();
        public PlayerCards Cards;
    }

    public class PlayerCards
    {
        public List<Card> Cards;
    }

    public class Card
    {
        public Card(string id)
        {
            this.Id = id;
            Variables[UnityEngine.Random.value.ToString()] = UnityEngine.Random.Range(0, 10);
        }
        public string Id;
        public Dictionary<string, int> Variables = new();
        public Dictionary<Type, Action> Effects = new();
    }

    public class Zone
    {
        public string Id;

        public int Limit; //sample Data
    }
    #endregion

    #region Turn Definition
    public class TurnPhase: IReference
    {
        public List<TurnStep> Steps = new List<TurnStep>();
    }

    public class TurnStep: IReference
    {

    }
    #endregion

    public class Match: IReference
    {
        public List<Player> Players = new List<Player>();
        public List<TurnPhase> Phases = new List<TurnPhase>();
    }
}
