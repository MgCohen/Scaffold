using UnityEngine;
using Sample.Turn;
using Sample.Turn.PlayerActions;
using System.Threading.Tasks;

namespace Sample.Turn.PlayWindows
{
    /// <summary>
    /// Sample play window: allows PlayCard, Activate, and Pass; closes when all players pass consecutively.
    /// </summary>
    public class MainPlayWindow : PlayWindow<ConsecutivePassWindowState>
    {
        public MainPlayWindow()
        {
            Register<PlayCardAction>(ValidatePlayCard, ExecutePlayCard);
            Register<ActivateAction>(ValidateActivate, ExecuteActivate);
            Register<PassAction>(ValidatePass, ExecutePass);
        }

        public override ConsecutivePassWindowState CreateInitialState()
        {
            return new ConsecutivePassWindowState(0);
        }

        private async Awaitable<bool> ValidatePlayCard(PlayCardAction action)
        {
            return true;
        }

        private async Awaitable ExecutePlayCard(PlayCardAction action)
        {
            //context.SetWindowState(new ConsecutivePassWindowState(0));
        }

        private async Awaitable<bool> ValidatePass(PassAction action)
        {
            return true;
        }

        private async Awaitable ExecutePass(PassAction action)
        {
            //var state = context.GetWindowState<ConsecutivePassWindowState>();
            //var newCount = state.ConsecutivePassCount + 1;
            //if (newCount >= 2)
            //{
            //    context.CloseWindow();
            //    return;
            //}
            //context.SetWindowState(state with { ConsecutivePassCount = newCount });
        }

        private async Awaitable<bool> ValidateActivate(ActivateAction action)
        {
            return true;
        }

        private async Awaitable ExecuteActivate(ActivateAction action)
        {
            //context.SetWindowState(new ConsecutivePassWindowState(0));
        }
    }
}
