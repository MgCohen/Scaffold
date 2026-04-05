using UnityEngine;

namespace Scaffold.CloudCode
{
    [CreateAssetMenu(fileName = "CloudCodeSettings", menuName = "Scaffold/Cloud Code/Settings", order = 0)]
    public sealed class CloudCodeSettings : ScriptableObject
    {
        public int MaxAttempts => maxAttempts;

        [SerializeField]
        [Min(1)]
        [Tooltip("Total attempts per call, including the first. Use 1 to disable retries.")]
        private int maxAttempts = 3;

        public int RetryDelayMilliseconds => retryDelayMilliseconds;

        [SerializeField]
        [Min(0)]
        [Tooltip("Base delay before retrying after CloudCodeRateLimitedException (milliseconds).")]
        private int retryDelayMilliseconds = 500;

        public bool ExponentialBackoff => exponentialBackoff;

        [SerializeField]
        [Tooltip("When enabled, multiplies delay by 2^(attempt-1) between attempts (capped).")]
        private bool exponentialBackoff;

        public bool LogCalls => logCalls;

        [SerializeField]
        [Tooltip("When enabled, logs module and endpoint only (no request/response payloads).")]
        private bool logCalls;

        public bool LogRawResponses => logRawResponses;

        [SerializeField]
        [Tooltip("When enabled, logs JSON response bodies. Avoid in production.")]
        private bool logRawResponses;

        public int TimeoutMilliseconds => timeoutMilliseconds;

        [SerializeField]
        [Min(0)]
        [Tooltip("Per-attempt timeout in milliseconds (0 = no timeout). Uses Task.WhenAny; the SDK call may continue after timeout.")]
        private int timeoutMilliseconds;

        public static CloudCodeSettings CreateDefault()
        {
            CloudCodeSettings loaded = Resources.Load<CloudCodeSettings>("CloudCodeSettings");
            return loaded != null ? loaded : CreateInstance<CloudCodeSettings>();
        }
    }
}
