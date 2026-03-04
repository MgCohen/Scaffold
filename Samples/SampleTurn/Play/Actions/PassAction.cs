using Sample.Turn;

namespace Sample.Turn.PlayerActions
{
    /// <summary>
    /// Player action representing a pass. Behaviour (pass count, priority, closure) is defined by the window's execute handler.
    /// </summary>
    public record PassAction(MatchPlayer Player) : PlayerAction(Player);
}
