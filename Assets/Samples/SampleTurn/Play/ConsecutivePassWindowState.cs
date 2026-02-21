namespace Sample.Turn
{
    /// <summary>
    /// Window state that tracks consecutive passes; used by MainPlayWindow to close when all players pass.
    /// </summary>
    public record ConsecutivePassWindowState(int ConsecutivePassCount) : PlayWindowState;
}
