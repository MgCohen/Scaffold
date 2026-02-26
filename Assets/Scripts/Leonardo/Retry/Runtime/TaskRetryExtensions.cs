using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Scaffold.RetryAwaitable.Shared
{
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
            return ExecuteAsTaskAsync().GetAwaiter();
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
            return ExecuteAsTaskAsync().GetAwaiter();
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
