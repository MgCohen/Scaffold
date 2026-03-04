using Sample.Turn;

namespace Sample.Turn.PlayerActions
{
    /// <summary>
    /// Player action for activating an ability. Stub. Behaviour is defined by the window.
    /// </summary>
    public record ActivateAction(MatchPlayer Player) : PlayerAction(Player);
}
