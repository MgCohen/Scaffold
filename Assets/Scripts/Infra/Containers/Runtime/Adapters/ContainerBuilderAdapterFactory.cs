using VContainer;

namespace Scaffold.Containers.Adapters
{
    /// <summary>
    /// Single factory for all container adapter creation. Swap backends by changing this type only.
    /// </summary>
    public static class ContainerBuilderAdapterFactory
    {
        public static IContainerBuilder CreateBuilder(VContainer.IContainerBuilder builder)
        {
            return new VContainerBuilderAdapter(builder);
        }

        public static IRegistrationBuilder<T> CreateRegistrationBuilder<T>(RegistrationBuilder inner)
        {
            return new VContainerRegistrationBuilderAdapter<T>(inner);
        }

        public static IContainerResolver CreateResolver(IObjectResolver inner)
        {
            return new VContainerResolverAdapter(inner);
        }
    }
}
