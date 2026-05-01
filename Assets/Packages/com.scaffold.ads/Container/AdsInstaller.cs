using VContainer;
using VContainer.Unity;

namespace Scaffold.Ads
{
    public sealed class AdsInstaller : IInstaller
    {
        public AdsInstaller(AdConfigurationSO adConfiguration)
        {
            this.adConfiguration = adConfiguration;
        }

        private readonly AdConfigurationSO adConfiguration;

        public void Install(IContainerBuilder builder)
        {
            builder.RegisterInstance(adConfiguration);

            builder.Register<RewardedAdManager>(Lifetime.Singleton);
            builder.Register<InterstitialAdManager>(Lifetime.Singleton);
            builder.Register<BannerAdManager>(Lifetime.Singleton);

            builder.Register<AdManager>(Lifetime.Singleton)
                .AsSelf()
                .AsImplementedInterfaces();
        }
    }
}
