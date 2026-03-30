using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Scaffold.Addressables.Contracts
{
    public interface IAssetGroupHandle<out T> : IDisposable where T : UnityEngine.Object
    {
        bool IsReleased { get; }
        bool IsReady { get; }
        Task WhenReady { get; }
        IReadOnlyList<T> Assets { get; }
        void Release();
    }
}
