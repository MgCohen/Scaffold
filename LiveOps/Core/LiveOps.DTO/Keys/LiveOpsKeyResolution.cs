#nullable enable

namespace LiveOps.DTO.Keys
{
    /// <summary>
    /// The storage/module key and optional GameApi wire key for a DTO type
    /// (<see cref="LiveOpsKeyResolver.GetModuleKey" />, <see cref="LiveOpsKeyResolver.GetWireKey" />).
    /// <see cref="Wire"/> is null for types that do not participate in wire dispatch (for example persistence/config DTOs).
    /// </summary>
    public readonly struct LiveOpsKeyResolution
    {
        public LiveOpsKeyResolution(string module, string? wire)
        {
            Module = module;
            Wire = wire;
        }

        public string Module { get; }

        public string? Wire { get; }
    }
}
