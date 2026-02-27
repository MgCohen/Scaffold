using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Scaffold.AwaitableRetry
{
    /// <summary>
    /// Provides extension methods for adding retry logic to Task and Task<T> delegates.
    /// The main goal is to convert standard functions into configurable RetryTaskBuilders easily.
    /// It is used across various asynchronous game systems, notably Cloud Code, to wrap volatile routines with resilient retries.
    /// </summary>
    public static class TaskRetryExtensions
    {
        public static RetryTaskBuilder<T> Retry<T>(this Func<Task<T>> operation, int maxRetries = 3)
        {
            return new RetryTaskBuilder<T>(operation, maxRetries);
        }

        public static RetryTaskBuilder Retry(this Func<Task> operation, int maxRetries = 3)
        {
            return new RetryTaskBuilder(operation, maxRetries);
        }
    }

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

        public RetryTaskBuilder(Func<Task<T>> operation, int maxRetries)
        {
            _operation = operation;
            _maxRetries = maxRetries;
        }

        public RetryTaskBuilder<T> WithCondition(Func<Exception, bool> condition)
        {
            _retryCondition = condition;
            return this;
        }

        public RetryTaskBuilder<T> WithDelay(float seconds)
        {
            _delaySeconds = seconds;
            return this;
        }

        public RetryTaskBuilder<T> OnRetry(Action<Exception, int> onRetry)
        {
            _onRetry = onRetry;
            return this;
        }

        public async Awaitable<T> ExecuteAsAwaitableAsync()
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
                        await Awaitable.WaitForSecondsAsync(_delaySeconds);
                    }
                    else
                    {
                        await Awaitable.NextFrameAsync();
                    }
                }
            }
            throw new InvalidOperationException("Retry loop exited unexpectedly.");
        }

        public System.Runtime.CompilerServices.TaskAwaiter<T> GetAwaiter()
        {
            Task<T> executedTask = ExecuteAsTaskAsync();
            return executedTask.GetAwaiter();
        }

        private async Task<T> ExecuteAsTaskAsync()
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

        public RetryTaskBuilder(Func<Task> operation, int maxRetries)
        {
            _operation = operation;
            _maxRetries = maxRetries;
        }

        public RetryTaskBuilder WithCondition(Func<Exception, bool> condition)
        {
            _retryCondition = condition;
            return this;
        }

        public RetryTaskBuilder WithDelay(float seconds)
        {
            _delaySeconds = seconds;
            return this;
        }

        public RetryTaskBuilder OnRetry(Action<Exception, int> onRetry)
        {
            _onRetry = onRetry;
            return this;
        }

        public async Awaitable ExecuteAsAwaitableAsync()
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
                        await Awaitable.WaitForSecondsAsync(_delaySeconds);
                    }
                    else
                    {
                        await Awaitable.NextFrameAsync();
                    }
                }
            }
            throw new InvalidOperationException("Retry loop exited unexpectedly.");
        }

        public System.Runtime.CompilerServices.TaskAwaiter GetAwaiter()
        {
            Task executedTask = ExecuteAsTaskAsync();
            return executedTask.GetAwaiter();
        }

        private async Task ExecuteAsTaskAsync()
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
    }
}
