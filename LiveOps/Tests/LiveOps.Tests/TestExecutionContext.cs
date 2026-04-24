using Unity.Services.CloudCode.Core;

namespace LiveOps.Tests
{
    internal sealed class TestExecutionContext : IExecutionContext
    {
        public string ProjectId { get; set; } = "project";
        public string PlayerId { get; set; } = "player";
        public string EnvironmentId { get; set; } = "env";
        public string EnvironmentName { get; set; } = "envName";
        public string AccessToken { get; set; } = "access";
        public string UserId { get; set; } = "user";
        public string Issuer { get; set; } = "issuer";
        public string ServiceToken { get; set; } = "service";
        public string AnalyticsUserId { get; set; } = "analytics";
        public string UnityInstallationId { get; set; } = "unity";
        public string CorrelationId { get; set; } = "corr";
    }
}
