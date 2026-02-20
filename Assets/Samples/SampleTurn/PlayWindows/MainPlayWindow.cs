using Sample.Turn;
using Sample.Turn.PlayerActions;

namespace Sample.Turn.PlayWindows
{
    /// <summary>
    /// Sample play window: allows PlayCard, Activate, and Pass; closes when all players pass consecutively.
    /// </summary>
    public class MainPlayWindow : PlayWindow
    {
        public MainPlayWindow()
        {
            Register<PlayCardAction>(ValidatePlayCard, ExecutePlayCard);
            Register<ActivateAction>(ValidateActivate, ExecuteActivate);
            Register<PassAction>(ValidatePass, ExecutePass);
        }

        public override PlayWindowState CreateInitialState()
        {
            return new ConsecutivePassWindowState(0);
        }

        private bool ValidatePlayCard(PlayCardAction action)
        {
            return true;
        }

        private void ExecutePlayCard(PlayCardAction action, IPlayWindowContext context)
        {
            context.SetWindowState(new ConsecutivePassWindowState(0));
            context.PassPriority();
        }

        private bool ValidatePass(PassAction action)
        {
            return true;
        }

        private void ExecutePass(PassAction action, IPlayWindowContext context)
        {
            var state = context.GetWindowState<ConsecutivePassWindowState>();
            var newCount = state.ConsecutivePassCount + 1;
            if (newCount >= 2)
            {
                context.CloseWindow();
                return;
            }
            context.SetWindowState(state with { ConsecutivePassCount = newCount });
            context.PassPriority();
        }

        private bool ValidateActivate(ActivateAction action)
        {
            return true;
        }

        private void ExecuteActivate(ActivateAction action, IPlayWindowContext context)
        {
            context.SetWindowState(new ConsecutivePassWindowState(0));
            context.PassPriority();
        }
    }
}
