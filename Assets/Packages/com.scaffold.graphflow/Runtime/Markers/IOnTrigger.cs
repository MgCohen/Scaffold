namespace Scaffold.GraphFlow
{
    /// <summary>
    /// Non-generic surface for the closed <see cref="OnTrigger{TEvent}"/> runtime node so
    /// callers (the per-package registry's bake factory, hosts, etc.) can read or write
    /// <see cref="Timing"/> without knowing the closed event type and without reflection.
    /// <para>The catalog hands back an opaque <see cref="RuntimeNode"/> from a parameterless
    /// factory; the caller casts to <see cref="IOnTrigger"/> to apply per-instance config.</para>
    /// </summary>
    public interface IOnTrigger
    {
        Timing Timing { get; set; }
    }
}
