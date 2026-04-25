using System;
using System.Collections.Generic;
using LiveOps.DTO.ModuleRequest;
using Newtonsoft.Json;

namespace LiveOps.DTO.GameApi
{

    public sealed class GameApiEnvelopeResponse
    {
        public string RequestKey { get; set; }

        public ResponseStatusType StatusType { get; set; }

        public string Message { get; set; } = string.Empty;

        [JsonProperty(TypeNameHandling = TypeNameHandling.Auto)]
        public ModuleResponse Result { get; set; }

        [JsonProperty(ItemTypeNameHandling = TypeNameHandling.Auto)]
        public List<ModuleResponse> NestedResponses { get; set; } = new List<ModuleResponse>();

        public static GameApiEnvelopeResponse Success(string key, ModuleResponse result, List<ModuleResponse> nested)
        {
            return new GameApiEnvelopeResponse
            {
                RequestKey = key,
                StatusType = ResponseStatusType.Success,
                Result = result,
                NestedResponses = nested ?? new List<ModuleResponse>(),
            };
        }

        public static GameApiEnvelopeResponse Exception(string key, Exception ex)
        {
            return new GameApiEnvelopeResponse
            {
                RequestKey = key,
                StatusType = ResponseStatusType.Exception,
                Message = ex?.Message ?? string.Empty,
            };
        }
    }
}
