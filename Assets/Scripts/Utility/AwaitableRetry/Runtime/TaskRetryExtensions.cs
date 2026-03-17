using System;
using System.Threading.Tasks;

namespace Scaffold.AwaitableRetry
{
    /// <summary>
    /// Provides extension methods for adding retry logic to Task and Task<T> delegates.
    /// The main goal is to convert standard functions into configurable RetryTaskBuilders easily.
    /// It is used across various asynchronous game systems, notably Cloud Code, to wrap volatile routines with resilient retries.
    /// </summary>
    public static class TaskRetryExtensions
    {
        /// <summary>
        /// Extends a generic task delegate with a retry wrapper.
        /// The main goal is to convert fragile tasks into resilient sequences.
        /// It is used implicitly when dot-chaining on task arrays.
        /// </summary>
        public static RetryTaskBuilder<T> Retry<T>(this Func<Task<T>> operation, int maxRetries = 3)
        {
            return new RetryTaskBuilder<T>(operation, maxRetries);
        }

        /// <summary>
        /// Extends a void task delegate with a retry wrapper.
        /// The main goal is to convert fragile operations into resilient pipelines.
        /// It is used when a side-effect needs to be ensured despite transient failures.
        /// </summary>
        public static RetryTaskBuilder Retry(this Func<Task> operation, int maxRetries = 3)
        {
            return new RetryTaskBuilder(operation, maxRetries);
        }
    }
}
