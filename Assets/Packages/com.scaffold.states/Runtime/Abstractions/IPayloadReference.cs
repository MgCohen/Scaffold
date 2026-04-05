#nullable enable

namespace Scaffold.States
{
    /// <summary>
    /// Optional: payload supplies the <see cref="IReference"/> key when the mutator is registered with <see cref="Reference.Null"/>.
    /// </summary>
    public interface IPayloadReference
    {
        IReference GetReference();
    }
}
