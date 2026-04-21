using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Scaffold.AppFlow
{
    public sealed class AppFlowProgress : IAppFlowProgress
    {
        private const int maxHistoryCount = 8;

        public AppFlowProgress(IAppFlowErrorHandler errorHandler)
        {
            this.errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
            this.errorHandler.OnError += OnErrorFromHandler;
        }

        public AppFlowSession Current
        {
            get
            {
                lock (gate)
                {
                    return BuildSessionSnapshot();
                }
            }
        }

        public IReadOnlyList<AppFlowSession> History
        {
            get
            {
                lock (gate)
                {
                    return history.ToArray();
                }
            }
        }

        private readonly IAppFlowErrorHandler errorHandler;

        private readonly object gate = new();

        private readonly List<LayerProgressEntry> entries = new();

        private readonly List<AppFlowSession> history = new();

        private string sessionName = string.Empty;

        private int totalLayers;

        private int completedLayers;

        private int failedLayers;

        private bool sessionActive;

        private AppFlowOutcome? sessionOutcome;

        private TaskCompletionSource<AppFlowOutcome> sessionTcs;

        private AppFlowOutcome lastOutcome;

        private bool hasLastOutcome;

        public event Action<AppFlowSession> Changed;

        public Task<AppFlowOutcome> WhenSessionCompleted()
        {
            lock (gate)
            {
                return ResolveWhenSessionCompletedLocked();
            }
        }

        internal void HostBeginSession(string name, int expectedTotalLayers)
        {
            lock (gate)
            {
                ResetSessionState(name, expectedTotalLayers);
            }

            RaiseChanged();
        }

        internal void HostEndSession(AppFlowOutcome outcome)
        {
            bool finalized = false;
            lock (gate)
            {
                if (!sessionActive)
                {
                    return;
                }

                FinalizeSessionLocked(outcome);
                finalized = true;
            }

            if (finalized)
            {
                RaiseChanged();
            }
        }

        internal int HostAddLayer(string layerName)
        {
            int index;
            lock (gate)
            {
                string name = layerName ?? string.Empty;
                entries.Add(new LayerProgressEntry(name, LayerStatus.Pending, 0f, null));
                index = entries.Count - 1;
            }

            RaiseChanged();
            return index;
        }

        internal void HostSetLayerStatus(int index, LayerStatus status)
        {
            bool mutated = false;
            lock (gate)
            {
                if (index < 0 || index >= entries.Count)
                {
                    return;
                }

                ApplyLayerStatusTransition(index, status);
                mutated = true;
            }

            if (mutated)
            {
                RaiseChanged();
            }
        }

        internal void HostSetSubProgress(int index, float subProgress)
        {
            bool mutated = false;
            lock (gate)
            {
                if (index < 0 || index >= entries.Count)
                {
                    return;
                }

                mutated = ApplySubProgressLocked(index, subProgress);
            }

            if (mutated)
            {
                RaiseChanged();
            }
        }

        private bool ApplySubProgressLocked(int index, float subProgress)
        {
            float clamped = Clamp01(subProgress);
            LayerProgressEntry e = entries[index];
            entries[index] = new LayerProgressEntry(e.LayerName, e.Status, clamped, e.LastError);
            return true;
        }

        private Task<AppFlowOutcome> ResolveWhenSessionCompletedLocked()
        {
            if (!sessionActive && hasLastOutcome)
            {
                return Task.FromResult(lastOutcome);
            }

            if (sessionTcs == null)
            {
                sessionTcs = new TaskCompletionSource<AppFlowOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            if (!sessionActive && sessionOutcome.HasValue)
            {
                sessionTcs.TrySetResult(sessionOutcome.Value);
            }

            return sessionTcs.Task;
        }

        private void ResetSessionState(string name, int expectedTotalLayers)
        {
            sessionName = name ?? string.Empty;
            totalLayers = expectedTotalLayers;
            completedLayers = 0;
            failedLayers = 0;
            entries.Clear();
            sessionActive = true;
            sessionOutcome = null;
            sessionTcs = new TaskCompletionSource<AppFlowOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        private void FinalizeSessionLocked(AppFlowOutcome outcome)
        {
            sessionActive = false;
            sessionOutcome = outcome;
            lastOutcome = outcome;
            hasLastOutcome = true;
            TrimHistoryIfNeeded();
            history.Add(BuildSessionSnapshot());
            sessionTcs?.TrySetResult(outcome);
            sessionTcs = null;
        }

        private void TrimHistoryIfNeeded()
        {
            if (history.Count >= maxHistoryCount)
            {
                history.RemoveAt(0);
            }
        }

        private void ApplyLayerStatusTransition(int index, LayerStatus status)
        {
            LayerProgressEntry e = entries[index];
            LayerStatus oldStatus = e.Status;
            float sub = e.SubProgress;
            if (status == LayerStatus.Ready || status == LayerStatus.Disposed)
            {
                sub = 1f;
            }

            entries[index] = new LayerProgressEntry(e.LayerName, status, sub, e.LastError);
            UpdateCountersOnStatusChange(oldStatus, status);
        }

        private void UpdateCountersOnStatusChange(LayerStatus oldStatus, LayerStatus status)
        {
            if (TryIncrementCompletedForReady(oldStatus, status))
            {
                return;
            }

            if (TryIncrementCompletedForDisposed(oldStatus, status))
            {
                return;
            }

            TryIncrementFailed(oldStatus, status);
        }

        private bool TryIncrementCompletedForReady(LayerStatus oldStatus, LayerStatus status)
        {
            if (status != LayerStatus.Ready || oldStatus == LayerStatus.Ready || oldStatus == LayerStatus.Failed)
            {
                return false;
            }

            completedLayers++;
            return true;
        }

        private bool TryIncrementCompletedForDisposed(LayerStatus oldStatus, LayerStatus status)
        {
            if (status != LayerStatus.Disposed || oldStatus == LayerStatus.Disposed)
            {
                return false;
            }

            completedLayers++;
            return true;
        }

        private void TryIncrementFailed(LayerStatus oldStatus, LayerStatus status)
        {
            if (status == LayerStatus.Failed && oldStatus != LayerStatus.Failed)
            {
                failedLayers++;
            }
        }

        private float Clamp01(float value)
        {
            if (value < 0f)
            {
                return 0f;
            }

            if (value > 1f)
            {
                return 1f;
            }

            return value;
        }

        private void OnErrorFromHandler(AppFlowErrorInfo info)
        {
            if (string.IsNullOrEmpty(info.LayerName))
            {
                return;
            }

            AttachErrorToMatchingLayer(info);
        }

        private void AttachErrorToMatchingLayer(AppFlowErrorInfo info)
        {
            if (TryAttachErrorLocked(info))
            {
                RaiseChanged();
            }
        }

        private bool TryAttachErrorLocked(AppFlowErrorInfo info)
        {
            bool changed = false;
            lock (gate)
            {
                int index = FindMatchingEntryIndex(info.LayerName);
                if (index >= 0)
                {
                    ApplyErrorToEntry(index, info);
                    changed = true;
                }
            }

            return changed;
        }

        private int FindMatchingEntryIndex(string layerName)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].LayerName == layerName)
                {
                    return i;
                }
            }

            return -1;
        }

        private void ApplyErrorToEntry(int index, AppFlowErrorInfo info)
        {
            LayerProgressEntry e = entries[index];
            entries[index] = new LayerProgressEntry(e.LayerName, e.Status, e.SubProgress, info);
        }

        private void RaiseChanged()
        {
            var handler = Changed;
            if (handler == null)
            {
                return;
            }

            AppFlowSession snap = CaptureSnapshotForSubscribers();
            InvokeChangedHandlerSafely(handler, snap);
        }

        private AppFlowSession CaptureSnapshotForSubscribers()
        {
            lock (gate)
            {
                return BuildSessionSnapshot();
            }
        }

        private void InvokeChangedHandlerSafely(Action<AppFlowSession> handler, AppFlowSession snap)
        {
            try
            {
                handler(snap);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[AppFlow] IAppFlowProgress.Changed subscriber threw: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private AppFlowSession BuildSessionSnapshot()
        {
            var snapshot = new List<LayerProgressEntry>(entries);
            LayerProgressEntry? current = FindCurrentEntry(snapshot);
            AppFlowOutcome? outcome = sessionOutcome;
            bool complete = !sessionActive && sessionOutcome.HasValue;
            return new AppFlowSession(sessionName, totalLayers, completedLayers, failedLayers, snapshot, current, complete, outcome);
        }

        private LayerProgressEntry? FindCurrentEntry(List<LayerProgressEntry> snapshot)
        {
            for (int i = 0; i < snapshot.Count; i++)
            {
                LayerStatus s = snapshot[i].Status;
                if (IsActiveStatus(s))
                {
                    return snapshot[i];
                }
            }

            return null;
        }

        private bool IsActiveStatus(LayerStatus s)
        {
            return s == LayerStatus.Pending || s == LayerStatus.Preparing || s == LayerStatus.Installing || s == LayerStatus.Initializing || s == LayerStatus.Disposing;
        }
    }
}
