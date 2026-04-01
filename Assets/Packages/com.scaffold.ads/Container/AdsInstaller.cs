using VContainer;
using VContainer.Unity;

namespace Scaffold.Ads
{
    public sealed class AdsInstaller : IInstaller
    {
        private readonly AdConfigurationSO _adConfiguration;

        public AdsInstaller(AdConfigurationSO adConfiguration)
        {
            _adConfiguration = adConfiguration;
        }

        public void Install(IContainerBuilder builder)
        {
            builder.RegisterInstance(_adConfiguration);

            builder.Register<RewardedAdManager>(Lifetime.Singleton);
            builder.Register<InterstitialAdManager>(Lifetime.Singleton);
            builder.Register<BannerAdManager>(Lifetime.Singleton);

            builder.Register<AdManager>(Lifetime.Singleton)
                .AsSelf()
                .AsImplementedInterfaces();
        }
    }
}
