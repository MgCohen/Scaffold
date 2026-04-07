namespace Scaffold.Entities.Samples
{
    /// <summary>
    /// Concrete behavior runner for the sample (Unity cannot serialize open generic <see cref="EntityBehaviorRunner{TData,TInput}"/> on prefabs).
    /// </summary>
    public sealed class SampleCharacterBehaviorRunner : EntityBehaviorRunner<Entity, SampleCharacterInput>
    {
    }
}
