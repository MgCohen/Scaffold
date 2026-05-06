#nullable enable

namespace Scaffold.States.Samples.CardGame
{
    public sealed record CardId(int Value) : Reference;

    public sealed record PlayerId(int Value) : Reference;
}
