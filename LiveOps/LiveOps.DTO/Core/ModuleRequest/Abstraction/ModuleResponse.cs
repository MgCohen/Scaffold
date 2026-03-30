using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace GameModuleDTO.ModuleRequests
{
    /// <summary>
    /// Serves as the base class for all module responses received from the network.
    /// </summary>
    public abstract class ModuleResponse
    {
        /// <summary>Gets the current status classification for the response.</summary>
        public ResponseStatusType StatusType { get; private set; }

        /// <summary>Gets the informative message string accompanying the response.</summary>
        public string Message { get; private set; } = string.Empty;

        /// <summary>Gets the collection of nested sub-responses.</summary>
        [JsonProperty(ItemTypeNameHandling = TypeNameHandling.Auto)]
        public List<ModuleResponse> Responses { get; protected set; } = new List<ModuleResponse>();

        /// <summary>
        /// Checks if the response completed with a successful status.
        /// </summary>
        /// <returns>True if the status signifies success.</returns>
        public bool IsSuccess()
        {
            return StatusType == ResponseStatusType.Success;
        }

        /// <summary>
        /// Assigns a specific status and descriptive message to the payload natively.
        /// </summary>
        /// <param name="status">The target status classification.</param>
        /// <param name="message">The accompanying descriptive literal.</param>
        public void SetResponse(ResponseStatusType status, string message)
        {
            StatusType = status;
            Message = message;
        }

        /// <summary>
        /// Assigns a standard failure status natively masking the reason safely.
        /// </summary>
        /// <param name="message">The failure reason string.</param>
        public void SetResponseFailure(string message)
        {
            SetResponse(ResponseStatusType.Failure, message);
        }

        /// <summary>
        /// Assigns a standard error status securely recording the issue natively.
        /// </summary>
        /// <param name="message">The error cause string.</param>
        public void SetResponseError(string message)
        {
            SetResponse(ResponseStatusType.Error, message);
        }

        /// <summary>
        /// Assigns an exception status securely appending the stack trace cleanly.
        /// </summary>
        /// <param name="message">The exception message payload.</param>
        public void SetResponseException(string message)
        {
            SetResponse(ResponseStatusType.Exception, $"Failed with exception: \n{message}");
        }

        /// <summary>
        /// Searches the nested response collection for a specific type safely.
        /// </summary>
        /// <typeparam name="T">The targeted response generic classification.</typeparam>
        /// <returns>The extracted response natively cast successfully.</returns>
        public T GetModuleResponse<T>() where T : ModuleResponse
        {
            return (T)Responses.FirstOrDefault(x => x.GetType() == typeof(T));
        }
    }
}