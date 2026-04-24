namespace LiveOps.DTO.ModuleRequest
{

    public abstract class ModuleRequest
    {

        public virtual string ModuleName => "LiveOps";

        public virtual string FunctionName => GetType().Name;
    }
}
