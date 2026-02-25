using GameModuleDTO.Keys;

namespace GameModuleDTO.ModuleRequests
{
    /// <summary>
    /// Serves as the base class for all module requests sent across the network.
    /// </summary>
    public abstract class ModuleRequest
    {
        /// <summary>Gets the authentication key required for the request.</summary>
        public virtual string AuthKey { get; protected set; }
        
        /// <summary>Gets the literal module name executing this request.</summary>
        public virtual string ModuleName { get { return ModuleKeys.DefaultModuleName; } }
        
        /// <summary>Gets the delay between retry attempts in seconds.</summary>
        public virtual int RetryCall { get; protected set; } = 2; // In seconds
        
        /// <summary>Gets the maximum allowed retry attempts.</summary>
        public virtual int MaxRetries { get; protected set; } = 2;

        /// <summary>Gets the resolved function name mapped natively to the type.</summary>
        public string FunctionName
        {
            get
            {
                return GetType().Name;
            }
        }

        /// <summary>Gets a value indicating whether an authentication key is present.</summary>
        public bool HasAuth { get { return !string.IsNullOrEmpty(AuthKey); } }

        /// <summary>
        /// Validates the module state natively before execution.
        /// </summary>
        public abstract void AssertModule();
    }
}