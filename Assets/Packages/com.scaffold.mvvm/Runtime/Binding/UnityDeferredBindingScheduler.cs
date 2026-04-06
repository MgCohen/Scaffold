using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Scaffold.MVVM.Binding
{
    /// <summary>
    /// Unity <see cref="MonoBehaviour"/> that implements <see cref="IDeferredBindingScheduler"/> using a coroutine pump.
    /// <see cref="BindingUpdateTiming.NextFrame"/> waits one frame (<c>yield return null</c>);
    /// <see cref="BindingUpdateTiming.EndOfFrame"/> uses <see cref="WaitForEndOfFrame"/>.
    /// </summary>
    public sealed class UnityDeferredBindingScheduler : MonoBehaviour, IDeferredBindingScheduler
    {
        public BindingUpdateTiming Mode
        {
            get => mode;
            set => mode = value;
        }

        [SerializeField]
        private BindingUpdateTiming mode = BindingUpdateTiming.NextFrame;

        private readonly Queue<Action> pending = new Queue<Action>();
        private bool pumpRunning;

        public void Schedule(Action continuation)
        {
            if (continuation is null)
            {
                throw new ArgumentNullException(nameof(continuation));
            }
            pending.Enqueue(continuation);
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
                    yield return YieldForMode();
                    DrainQueue();
                }
            }
            finally
            {
                pumpRunning = false;
            }
        }

        private object YieldForMode()
        {
            return mode == BindingUpdateTiming.EndOfFrame
                ? new WaitForEndOfFrame()
                : null;
        }

        private void DrainQueue()
        {
            while (pending.Count > 0)
            {
                pending.Dequeue().Invoke();
            }
        }
    }
}
