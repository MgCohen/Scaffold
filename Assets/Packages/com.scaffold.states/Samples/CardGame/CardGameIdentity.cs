#nullable enable

namespace Scaffold.States.Samples.CardGame
{
    public sealed record CardId(int Value) : IReference;

    public sealed record PlayerId(int Value) : IReference;
}
