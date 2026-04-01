namespace GameModuleDTO.ModuleRequests
{
    /// <summary>
    /// Serves as the base class for all module requests sent across the network.
    /// </summary>
    public abstract class ModuleRequest
    {
        /// <summary>Gets the literal module name executing this request.</summary>
        public virtual string ModuleName => "LiveOps";
        
        /// <summary>Gets the resolved function name mapped natively to the type.</summary>
        public virtual string FunctionName => GetType().Name;
    }
}