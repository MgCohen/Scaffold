using Sample.Turn;

namespace Sample.Turn.PlayerActions
{
    /// <summary>
    /// Player action for playing a card. Stub; real game would carry a Card reference. Behaviour is defined by the window.
    /// </summary>
    public record PlayCardAction(MatchPlayer Player) : PlayerAction(Player);
}
