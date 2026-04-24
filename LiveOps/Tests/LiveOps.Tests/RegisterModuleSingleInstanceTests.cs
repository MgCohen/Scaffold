using System.Threading;
using System.Threading.Tasks;
using LiveOps.DTO.GameModule;
using LiveOps.GameApi;
using LiveOps.GameModule;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LiveOps.Tests
{
    public sealed class RegisterModuleSingleInstanceTests
    {
        private sealed class SampleData : IGameModuleData
        {
            public string Key => nameof(SampleData);
        }

        private sealed class SampleModule : GameModule<SampleData>
        {
            public override Task<IGameModuleData> InitializeAsync(
                GameApiSession session,
                CancellationToken cancellationToken = default) =>
                Task.FromResult<IGameModuleData>(new SampleData());
        }

        [Fact]
        public void Scopes_resolve_IGameModule_and_TModule_to_same_instance()
        {
            IServiceCollection services = new ServiceCollection();
            services.AddScoped<SampleModule>();
            services.AddScoped<IGameModule>(sp => sp.GetRequiredService<SampleModule>());

            using ServiceProvider sp = services.BuildServiceProvider();
            using IServiceScope scope = sp.CreateScope();
            IGameModule? a = scope.ServiceProvider.GetService<IGameModule>();
            SampleModule? b = scope.ServiceProvider.GetService<SampleModule>();

            Assert.NotNull(a);
            Assert.NotNull(b);
            Assert.Same(a, b);
        }
    }
}
