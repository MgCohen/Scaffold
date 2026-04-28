namespace Scaffold.Entities
{
    public sealed class IncrementingInstanceIdGenerator : IInstanceIdGenerator
    {
        private int nextId = 1;

        public InstanceId Next()
        {
            return new InstanceId(nextId++);
        }
    }
}
