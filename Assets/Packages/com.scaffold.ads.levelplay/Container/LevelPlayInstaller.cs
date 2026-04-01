using VContainer;
using VContainer.Unity;

namespace Scaffold.Ads.Levelplay
{
    public sealed class LevelPlayInstaller : IInstaller
    {
        private readonly LevelPlayAdConfigurationSO _adConfiguration;

        public LevelPlayInstaller(LevelPlayAdConfigurationSO adConfiguration)
        {
            _adConfiguration = adConfiguration;
        }

        public void Install(IContainerBuilder builder)
        {
            builder.RegisterInstance(_adConfiguration).As<AdConfigurationSO>().AsSelf();
            new AdsInstaller(_adConfiguration).Install(builder);
        }
    }
}
