using Scaffold.States;
using System.Collections.Generic;
using UnityEngine;

namespace Scaffold.Game.Stack
{


    public class PlayService
    {
        public PlayService(Store store, IStackHandler stack, IPriorityHandler priority, IActionHandler actions, ITurnHandler turn)
        {
            this.store = store;
            this.stack = stack;
            this.priority = priority;
            this.actions = actions;
            this.turn = turn;
        }

        private Store store;
        private IStackHandler stack;
        private IPriorityHandler priority;
        private IActionHandler actions;
        private ITurnHandler turn;

        public void OpenWindow()
        {
            IPlayer player = turn.GetActivePlayer();
            priority.SetPriority(player);
        }

        public void Play(IStackable stackable)
        {
            //assume Cost/Target and other things have been done outside of stack control
            stack.Push(stackable);
            //Pass is not automatic here
        }

        public void Pass()
        {
            priority.PassPriority();
            if (priority.CheckForPlayerPriority())
            {
                IPlayer player = priority.GetPriorityPlayer();
                //notify?
            }
            else
            {
                CloseWindow();
            }
        }

        private void CloseWindow()
        {
            ResolveStack();
        }

        private void ResolveStack()
        {
            if (stack.IsStackEmpty())
            {
                return;
            }
            ResolveNext();
            stack.Dispose();
        }

        private void ResolveNext()
        {
            IStackable stackable = stack.GetNext();
            Resolve(stackable);
        }

        private void Resolve(IStackable stackable)
        {

        }
    }

    #region Models

    public interface ICommand
    {

    }

    public interface IStackable
    {
        bool Validate();

        void Resolve();

        void Dispose();
    }

    #endregion

    //Who opens window?
    //GamePhase opens main window
    //---- opens reaction window

    //Quem valida se uma ação pode ser feita?
    //Quem diz que uma ação foi feita?
    //Quem diz que uma ação terminou?
}