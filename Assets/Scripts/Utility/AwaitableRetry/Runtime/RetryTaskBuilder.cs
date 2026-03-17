using System;
using System.Threading.Tasks;

namespace Scaffold.AwaitableRetry
{
    /// <summary>
    /// Configures and executes an asynchronous task with predefined retry and delay policies, returning a result.
    /// The main goal is to safely attempt network or volatile calls and correctly backoff between failures.
    /// It is used by infrastructure services when continuous execution until success or max bounds is necessary.
    /// </summary>
    public class RetryTaskBuilder<T>
    {
        private readonly Func<Task<T>> _operation;
        private readonly int _maxRetries;
        private Func<Exception, bool> _retryCondition = _ => true;
        private Action<Exception, int> _onRetry = null;
        private float _delaySeconds = 0f;

        /// <summary>
        /// Instantiates the builder with minimum properties.
        /// The main goal is to prepare the loop.
        /// It is used organically by the extension method.
        /// </summary>
        public RetryTaskBuilder(Func<Task<T>> operation, int maxRetries)
        {
            _operation = operation;
            _maxRetries = maxRetries;
        }

        /// <summary>
        /// Restricts what exceptions trigger a retry.
        /// The main goal is to bail fast on permanent errors like 4xx.
        /// It is used to apply custom validation dynamically.
        /// </summary>
        public RetryTaskBuilder<T> WithCondition(Func<Exception, bool> condition)
        {
            _retryCondition = condition;
            return this;
        }

        /// <summary>
        /// Adds a delay duration between retries.
        /// The main goal is to throttle rapid requests.
        /// It is used when servers might be overloaded or rate-limiting.
        /// </summary>
        public RetryTaskBuilder<T> WithDelay(float seconds)
        {
            _delaySeconds = seconds;
            return this;
        }

        /// <summary>
        /// Executes a callback every time a retry cycle is kicked off.
        /// The main goal is to allow external logging interfaces to track flakiness.
        /// It is used by debugging tools natively.
        /// </summary>
        public RetryTaskBuilder<T> OnRetry(Action<Exception, int> onRetry)
        {
            _onRetry = onRetry;
            return this;
        }

        /// <summary>
        /// Flushes the configured operation, returning a Task.
        /// The main goal is to safely resolve or ultimately throw after maximum attempts.
        /// It is used natively within the final sequence block.
        /// </summary>
        public async Task<T> ExecuteAsync()
        {
            for (int i = 0; i <= _maxRetries; i++)
            {
                try
                {
                    return await _operation();
                }
                catch (Exception ex)
                {
                    if (i == _maxRetries || !_retryCondition(ex))
                    {
                        throw;
                    }

                    _onRetry?.Invoke(ex, i + 1);

                    if (_delaySeconds > 0)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(_delaySeconds));
                    }
                    else
                    {
                        await Task.Yield();
                    }
                }
            }
            throw new InvalidOperationException("Retry loop exited unexpectedly.");
        }

        /// <summary>
        /// Re-exposes the Awaiter from standard tasks natively.
        /// The main goal is to remain compatible with standard await clauses implicitly.
        /// It is used silently by the compiler when awaiting the builder.
        /// </summary>
        public System.Runtime.CompilerServices.TaskAwaiter<T> GetAwaiter()
        {
            Task<T> executedTask = ExecuteAsync();
            return executedTask.GetAwaiter();
        }
    }

    /// <summary>
    /// Configures and executes an asynchronous task with predefined retry and delay policies without returning a result.
    /// The main goal is to safely attempt void-like network or volatile calls and correctly backoff between failures.
    /// It is used by infrastructure services when continuous execution until success or max bounds is logically required.
    /// </summary>
    public class RetryTaskBuilder
    {
        private readonly Func<Task> _operation;
        private readonly int _maxRetries;
        private Func<Exception, bool> _retryCondition = _ => true;
        private Action<Exception, int> _onRetry = null;
        private float _delaySeconds = 0f;

        /// <summary>
        /// Instantiates the generic-less builder with minimum properties.
        /// The main goal is to prepare the loop.
        /// It is used organically by the extension method.
        /// </summary>
        public RetryTaskBuilder(Func<Task> operation, int maxRetries)
        {
            _operation = operation;
            _maxRetries = maxRetries;
        }

        /// <summary>
        /// Restricts what exceptions trigger a retry.
        /// The main goal is to bail fast on permanent errors.
        /// It is used to apply custom validation dynamically.
        /// </summary>
        public RetryTaskBuilder WithCondition(Func<Exception, bool> condition)
        {
            _retryCondition = condition;
            return this;
        }

        /// <summary>
        /// Adds a delay duration between retries.
        /// The main goal is to throttle rapid requests.
        /// It is used when servers might be overloaded or rate-limiting.
        /// </summary>
        public RetryTaskBuilder WithDelay(float seconds)
        {
            _delaySeconds = seconds;
            return this;
        }

        /// <summary>
        /// Executes a callback every time a retry cycle is kicked off.
        /// The main goal is to allow external logging interfaces to track flakiness.
        /// It is used by debugging tools.
        /// </summary>
        public RetryTaskBuilder OnRetry(Action<Exception, int> onRetry)
        {
            _onRetry = onRetry;
            return this;
        }

        /// <summary>
        /// Flushes the configured operation utilizing the retry variables, without returning a result.
        /// The main goal is to safely resolve or ultimately throw after maximum attempts.
        /// It is used natively within the final sequence block.
        /// </summary>
        public async Task ExecuteAsync()
        {
            for (int i = 0; i <= _maxRetries; i++)
            {
                try
                {
                    await _operation();
                    return;
                }
                catch (Exception ex)
                {
                    if (i == _maxRetries || !_retryCondition(ex))
                    {
                        throw;
                    }

                    _onRetry?.Invoke(ex, i + 1);

                    if (_delaySeconds > 0)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(_delaySeconds));
                    }
                    else
                    {
                        await Task.Yield();
                    }
                }
            }
            throw new InvalidOperationException("Retry loop exited unexpectedly.");
        }

        /// <summary>
        /// Re-exposes the Awaiter from standard tasks natively.
        /// The main goal is to remain compatible with standard await clauses implicitly.
        /// It is used silently by the compiler when awaiting the builder.
        /// </summary>
        public System.Runtime.CompilerServices.TaskAwaiter GetAwaiter()
        {
            Task executedTask = ExecuteAsync();
            return executedTask.GetAwaiter();
        }
    }
}