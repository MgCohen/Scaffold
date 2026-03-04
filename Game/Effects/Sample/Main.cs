using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace Scaffold.Effects
{
    public class Main : MonoBehaviour
    {
        private EffectDirector handler;
        private List<EffectSource> cardsInHand = new List<EffectSource>();

        private void Update()
        {
            if (Input.GetKey(KeyCode.Space))
            {
                var card = cardsInHand.First();
                Play(card);
            }
        }

        private void OnTurnStart()
        {
            foreach(var card in cardsInHand)
            {
                //check play rules
                //check if its player turn -> Game Phase
                //check if action is allowed -> Game Step
                //check if effect is valid -> Entity
                //highlight cards
            }
        }

        private void Play(EffectSource card)
        {
            var effect = card.GetEntryPointEffect<OnPlay>();
            Play(effect);
        }

        public void Play(Effect effect)
        {
            handler.Execute(effect);
        }
    }


    public class Fireball : EffectSource
    {
        public void Register()
        {
            //builder.RegisterPlayEffect()
            //       .WithCost(() => ThatCost)
            //       .WithCondition(() => ThatCondition)
            //       .Do(() => Execute(new ACommand());

            //builder.RegisterReaction<OnDamage>()
            //       .WithCost(() => SpendLife)
            //       .Do(t => t.x = 1);
        }

        //public override async Task Execute()
        //{
        //    var a = await ExecuteCommand(new ACommand());
        //    if (a != null)
        //    {
        //        var b = await ExecuteCommand(new BCommand());
        //    }
        //    else
        //    {
        //        var a2 = await ExecuteCommand(new ACommand());
        //    }
        //}

        //public override Task<bool> Validate()
        //{
        //    //if 
        //    throw new System.NotImplementedException();
        //}
    }

    public class ACommand : Command
    {
        public override Task Execute()
        {
            throw new System.NotImplementedException();
        }
    }

    public class BCommand : Command
    {
        public override Task Execute()
        {
            throw new System.NotImplementedException();
        }
    }

    public class OnPlay: EntryPoint
    {

    }

    public class Match
    {
        public bool Validate()
        {
            return true;
        }
    }

    public record MatchState
    {

    }
}
