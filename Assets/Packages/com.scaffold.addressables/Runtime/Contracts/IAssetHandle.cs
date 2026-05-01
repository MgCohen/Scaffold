using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Scaffold.Addressables.Contracts
{
    public interface IAssetHandle
    {
        Type AssetType { get; }
        UnityEngine.Object UntypedAsset { get; }
        bool IsReleased { get; }
        AssetHandleState State { get; }
        bool IsReady { get; }
        Task WhenReady { get; }
        void Release();
    }
}

