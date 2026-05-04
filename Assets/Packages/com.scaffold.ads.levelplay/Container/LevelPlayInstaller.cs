using VContainer;
using VContainer.Unity;

namespace Scaffold.Ads.Levelplay
{
    public sealed class LevelPlayInstaller : IInstaller
    {
        public LevelPlayInstaller(LevelPlayAdConfigurationSO adConfiguration)
        {
            this.adConfiguration = adConfiguration;
        }

        private readonly LevelPlayAdConfigurationSO adConfiguration;

        public void Install(IContainerBuilder builder)
        {
            new AdsInstaller(adConfiguration).Install(builder);
        }
    }
}
