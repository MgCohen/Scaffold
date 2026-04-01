using System;
using Scaffold.Navigation.Contracts;
using Scaffold.Schemas;
using Scaffold.Types;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Scaffold.Navigation
{
    [CreateAssetMenu(menuName = "Modules/Navigation/View Config")]
    [SchemaFilter(typeof(ViewSchema))]
    public class ViewConfig : SchemaObject
    {
        public AssetReference Asset => asset;
        [SerializeField] private AssetReference asset;

        public Type ViewType => viewType.Type;
        [SerializeField, TypeReferenceFilter(typeof(IView))] private TypeReference viewType;

        public Type ControllerType => controllerType.Type;
        [SerializeField, TypeReferenceFilter(typeof(IViewController))] private TypeReference controllerType;

#if UNITY_EDITOR
        public void OnValidate()
        {
            if (asset == null || asset.editorAsset == null)
            {
                viewType = null;
                controllerType = null;
                return;
            }
            SetTypeFromAsset();
        }

        private void SetTypeFromAsset()
        {
            GameObject viewObject = asset.editorAsset as GameObject;
            Type resolvedViewType = viewObject?.GetComponent<IView>()?.GetType();
            ApplyViewType(resolvedViewType);
        }
#endif

        public void SetType(Type viewType)
        {
            try
            {
                ApplyViewType(viewType);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void ApplyViewType(Type viewType)
        {
            if (viewType == null)
            {
                this.viewType = null;
                controllerType = null;
                return;
            }
            this.viewType = new TypeReference(viewType);
            Type controller = ResolveControllerType(viewType);
            controllerType = controller == null ? null : new TypeReference(controller);
        }

        private Type ResolveControllerType(Type viewType)
        {
            Type baseType = viewType.BaseType;
            if (baseType == null || !baseType.IsGenericType)
            {
                return null;
            }
            Type[] genericArguments = baseType.GenericTypeArguments;
            if (genericArguments.Length == 0)
            {
                return null;
            }
            return genericArguments[0];
        }
    }
}

