using System;
using System.Collections.Generic;
using LiveOps.DTO.ModuleRequest;
using GameDataPayload = LiveOps.DTO.GameModule.GameData;

namespace LiveOpsModules.DTO.GameData
{
    /// <summary>Per-module error when a partial <see cref="GameDataResponse" /> is returned.</summary>
    public sealed class GameDataModuleError
    {
        public string ModuleKey { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Serves as the standard response delivering the gathered game module payload block.
    /// </summary>
    public class GameDataResponse : ModuleResponse
    {
        /// <summary>Gets the core game data configuration mapped over the network.</summary>
        public GameDataPayload GameData { get; protected set; }

        /// <summary>True if one or more modules failed during aggregation.</summary>
        public bool IsPartial { get; set; }

        /// <summary>Populated when <see cref="IsPartial" /> is true.</summary>
        public IReadOnlyList<GameDataModuleError> ModuleLoadErrors { get; set; } = Array.Empty<GameDataModuleError>();

        /// <summary>
        /// Initializes a newly received data block efficiently safely.
        /// </summary>
        public GameDataResponse(
            GameDataPayload gameData,
            bool isPartial = false,
            IReadOnlyList<GameDataModuleError>? moduleLoadErrors = null)
        {
            GameData = gameData;
            IsPartial = isPartial;
            if (moduleLoadErrors is { Count: > 0 })
            {
                IsPartial = true;
                ModuleLoadErrors = moduleLoadErrors;
            }
        }
    }
}