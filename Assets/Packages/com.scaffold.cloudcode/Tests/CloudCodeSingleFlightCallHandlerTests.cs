using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Scaffold.CloudCode;

namespace Scaffold.CloudCode.Tests
{
    /// <summary>
    /// Ensures <see cref="CloudCodeSingleFlightCallHandler"/> serializes real invocations per module name.
    /// </summary>
    public sealed class CloudCodeSingleFlightCallHandlerTests
    {
        [Test]
        public async Task SameModule_SecondCallDoesNotEnterInnerUntilFirstCompletes()
        {
            BlockingInner inner = new BlockingInner();
            CloudCodeSingleFlightCallHandler singleFlight = new CloudCodeSingleFlightCallHandler(inner);

            Task<string> first = singleFlight.InvokeAsync("moduleA", "endpoint1", new Dictionary<string, object>(), CancellationToken.None);
            await inner.WaitForFirstInnerStartedAsync();

            Task<string> second = singleFlight.InvokeAsync("moduleA", "endpoint2", new Dictionary<string, object>(), CancellationToken.None);
            await Task.Delay(30);

            Assert.That(inner.InnerInvokeCount, Is.EqualTo(1), "second call must wait on the per-module gate before reaching the inner handler");

            inner.ReleaseFirstCall();
            await first;
            await second;

            Assert.That(inner.InnerInvokeCount, Is.EqualTo(2));
        }

        private sealed class BlockingInner : ICloudCodeCallHandler
        {
            private readonly TaskCompletionSource<bool> firstStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly TaskCompletionSource<bool> releaseFirst = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            private int innerInvokeCount;

            public int InnerInvokeCount => Volatile.Read(ref innerInvokeCount);

            public Task WaitForFirstInnerStartedAsync() => firstStarted.Task;

            public void ReleaseFirstCall() => releaseFirst.TrySetResult(true);

            public async Task<string> InvokeAsync(string module, string endpoint, Dictionary<string, object> payload, CancellationToken cancellationToken)
            {
                int n = Interlocked.Increment(ref innerInvokeCount);
                if (n == 1)
                {
                    firstStarted.TrySetResult(true);
                    await releaseFirst.Task.ConfigureAwait(false);
                }

                return "{}";
            }
        }
    }
}
