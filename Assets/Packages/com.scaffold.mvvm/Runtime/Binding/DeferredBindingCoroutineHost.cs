using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Scaffold.MVVM.Binding
{
    /// <summary>
    /// Global Unity host for deferred binding flushes: a single lazy-created DontDestroyOnLoad
    /// <see cref="MonoBehaviour"/> that runs a coroutine pump. No dependency injection.
    /// </summary>
    public static class DeferredBindingCoroutineHost
    {
        private static readonly Action<Action, BindingUpdateTiming> defaultScheduleCore = DefaultSchedule;

        /// <summary>
        /// Test seam: replace with a queue-based implementation; reset with <see cref="ResetScheduleCoreForTests"/>.
        /// </summary>
        internal static Action<Action, BindingUpdateTiming> ScheduleCore = defaultScheduleCore;

        /// <summary>
        /// Queue work to run after the yield implied by <paramref name="timing"/> (ignored for <see cref="BindingUpdateTiming.Immediate"/>, which runs synchronously).
        /// </summary>
        public static void Schedule(Action continuation, BindingUpdateTiming timing)
        {
            if (continuation is null)
            {
                throw new ArgumentNullException(nameof(continuation));
            }

            ScheduleCore(continuation, timing);
        }

        internal static void ResetScheduleCoreForTests()
        {
            ScheduleCore = defaultScheduleCore;
        }

        private static void DefaultSchedule(Action continuation, BindingUpdateTiming timing)
        {
            if (timing == BindingUpdateTiming.Immediate)
            {
                continuation();
                return;
            }

            Host.Instance.Enqueue(continuation, timing);
        }

        private sealed class Host : MonoBehaviour
        {
            internal static Host Instance
            {
                get
                {
                    if (instance != null)
                    {
                        return instance;
                    }

                    var go = new GameObject(nameof(DeferredBindingCoroutineHost))
                    {
                        hideFlags = HideFlags.HideAndDontSave,
                    };
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<Host>();
                    return instance;
                }
            }

            private static Host instance;

            private readonly Queue<(Action continuation, BindingUpdateTiming timing)> pending =
                new Queue<(Action, BindingUpdateTiming)>();

            private bool pumpRunning;

            internal void Enqueue(Action continuation, BindingUpdateTiming timing)
            {
                pending.Enqueue((continuation, timing));
                if (!pumpRunning)
                {
                    StartCoroutine(PumpCoroutine());
                }
            }

            private IEnumerator PumpCoroutine()
            {
                pumpRunning = true;
                try
                {
                    while (pending.Count > 0)
                    {
                        BindingUpdateTiming waveTiming = pending.Peek().timing;
                        yield return ToYieldInstruction(waveTiming);
                        DrainQueue();
                    }
                }
                finally
                {
                    pumpRunning = false;
                }
            }

            private void DrainQueue()
            {
                while (pending.Count > 0)
                {
                    pending.Dequeue().continuation.Invoke();
                }
            }

            private object ToYieldInstruction(BindingUpdateTiming timing)
            {
                return timing == BindingUpdateTiming.EndOfFrame
                    ? new WaitForEndOfFrame()
                    : null;
            }
        }
    }
}
