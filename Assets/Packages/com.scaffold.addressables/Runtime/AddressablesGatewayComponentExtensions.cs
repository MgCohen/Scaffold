using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Scaffold.Addressables.Contracts;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Scaffold.Addressables
{
    public static class AddressablesGatewayComponentExtensions
    {
        public static async Task<IAssetHandle<TComponent>> LoadComponentAsync<TComponent>(this IAddressablesGateway gateway, AssetReference reference, CancellationToken cancellationToken = default) where TComponent : Component
        {
            GuardGateway(gateway);
            IAssetHandle<GameObject> gameObjectHandle = await gateway.LoadAsync<GameObject>(reference, cancellationToken);
            try
            {
                return await CompleteComponentHandleAsync<TComponent>(gameObjectHandle, cancellationToken);
            }
            catch
            {
                gameObjectHandle.Release();
                throw;
            }
        }

        public static Task<IAssetHandle<TComponent>> LoadComponentAsync<TComponent>(this IAddressablesGateway gateway, AssetReferenceT<GameObject> reference, CancellationToken cancellationToken = default) where TComponent : Component
        {
            return gateway.LoadComponentAsync<TComponent>((AssetReference)reference, cancellationToken);
        }

        public static async Task<IAssetGroupHandle<TComponent>> LoadComponentGroupAsync<TComponent>(this IAddressablesGateway gateway, AssetLabelReference label, CancellationToken cancellationToken = default) where TComponent : Component
        {
            GuardGateway(gateway);
            IAssetGroupHandle<GameObject> gameObjectGroup = await gateway.LoadAsync<GameObject>(label, cancellationToken);
            try
            {
                return await CompleteComponentGroupAsync<TComponent>(gameObjectGroup, cancellationToken);
            }
            catch
            {
                gameObjectGroup.Release();
                throw;
            }
        }

        private static void GuardGateway(IAddressablesGateway gateway)
        {
            if (gateway == null)
            {
                throw new ArgumentNullException(nameof(gateway));
            }
        }

        private static async Task<IAssetHandle<TComponent>> CompleteComponentHandleAsync<TComponent>(IAssetHandle<GameObject> gameObjectHandle, CancellationToken cancellationToken) where TComponent : Component
        {
            await gameObjectHandle.WhenReady;
            cancellationToken.ThrowIfCancellationRequested();
            GameObject gameObject = gameObjectHandle.Asset;
            TComponent component = gameObject.GetComponent<TComponent>();
            if (component == null)
            {
                throw new InvalidOperationException($"Loaded asset '{gameObject.name}' does not contain required component '{typeof(TComponent).Name}'.");
            }

            return new ComponentAssetHandle<TComponent>(gameObjectHandle, component);
        }

        private static async Task<IAssetGroupHandle<TComponent>> CompleteComponentGroupAsync<TComponent>(IAssetGroupHandle<GameObject> gameObjectGroup, CancellationToken cancellationToken) where TComponent : Component
        {
            await gameObjectGroup.WhenReady;
            cancellationToken.ThrowIfCancellationRequested();
            List<TComponent> collected = CollectComponents<TComponent>(gameObjectGroup.Assets);
            return new ComponentGroupHandle<TComponent>(gameObjectGroup, collected);
        }

        private static List<TComponent> CollectComponents<TComponent>(IReadOnlyList<GameObject> gameObjects) where TComponent : Component
        {
            List<TComponent> components = new List<TComponent>(gameObjects.Count);
            for (int i = 0; i < gameObjects.Count; i++)
            {
                GameObject gameObject = gameObjects[i];
                TComponent component = gameObject != null ? gameObject.GetComponent<TComponent>() : null;
                if (component == null)
                {
                    throw new InvalidOperationException($"Loaded asset at index {i} does not contain required component '{typeof(TComponent).Name}'.");
                }

                components.Add(component);
            }

            return components;
        }
    }
}
