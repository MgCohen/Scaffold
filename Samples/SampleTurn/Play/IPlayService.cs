using System;
using UnityEngine;

namespace Sample.Turn
{
    /// <summary>
    /// Service for managing play windows and executing player actions.
    /// </summary>
    public interface IPlayService
    {
        void OpenWindow(PlayWindow window, Action onClosed);
        void CloseWindow();
        Awaitable ExecuteAction(PlayerAction action);
        Awaitable<bool> ValidateAction(PlayerAction action);
    }
}
