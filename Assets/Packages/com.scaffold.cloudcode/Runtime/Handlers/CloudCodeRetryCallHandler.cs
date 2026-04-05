using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.CloudCode;
using UnityEngine;

namespace Scaffold.CloudCode
{
    /// <summary>
    /// Retries the inner pipeline on <see cref="CloudCodeRateLimitedException"/> according to <see cref="CloudCodeSettings"/>.
    /// </summary>
    internal sealed class CloudCodeRetryCallHandler : ICloudCodeCallHandler
    {
        internal CloudCodeRetryCallHandler(CloudCodeSettings settings, ICloudCodeCallHandler inner)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        private readonly CloudCodeSettings settings;
        private readonly ICloudCodeCallHandler inner;

        public async Task<string> InvokeAsync(string module, string endpoint, Dictionary<string, object> payload, CancellationToken cancellationToken)
        {
            int maxAttempts = Mathf.Max(1, settings.MaxAttempts);
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                LogAttemptIfEnabled(module, endpoint, attempt, maxAttempts);
                try
                {
                    return await inner.InvokeAsync(module, endpoint, payload, cancellationToken);
                }
                catch (CloudCodeRateLimitedException) when (attempt < maxAttempts)
                {
                    await DelayBeforeRetryAsync(attempt, cancellationToken);
                }
            }

            throw new InvalidOperationException("Cloud Code retry loop exited without a successful response.");
        }

        private void LogAttemptIfEnabled(string module, string endpoint, int attempt, int maxAttempts)
        {
            if (settings.LogCalls)
            {
                Debug.Log($"[CloudCode] module '{module}' endpoint '{endpoint}' (attempt {attempt}/{maxAttempts})");
            }
        }

        private async Task DelayBeforeRetryAsync(int attemptIndex, CancellationToken cancellationToken)
        {
            int baseMs = settings.RetryDelayMilliseconds;
            int delayMs = settings.ExponentialBackoff ? Mathf.Min(baseMs * (1 << (attemptIndex - 1)), 30000) : baseMs;
            if (delayMs > 0)
            {
                await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
