using Scaffold.MVVM;
using Scaffold.Schemas;
using Scaffold.Types;
using System;
using UnityEngine;

#if ADDRESSABLES
using UnityEngine.AddressableAssets;
#endif

namespace Scaffold.Navigation
{

    [CreateAssetMenu(menuName = "Modules/Navigation/View Config")]
    [SchemaFilter(typeof(ViewSchema))]
    public class ViewConfig : SchemaObject
    {
#if ADDRESSABLES
        public AssetReference Asset => asset;
        [SerializeField] protected AssetReference asset;
#else
        public GameObject ViewAsset => viewAsset;
        [SerializeField] protected GameObject viewAsset;
#endif
        public Type ViewType => viewType.Type;
        [SerializeField, TypeReferenceFilter(typeof(IView))] protected TypeReference viewType;

        public Type ControllerType => controllerType.Type;
        [SerializeField, TypeReferenceFilter(typeof(IViewController))] protected TypeReference controllerType;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (viewAsset == null)
            {
                viewType = null;
                controllerType = null;
                return;
            }
#if ADDRESSABLES
        viewType = new TypeReference((asset?.editorAsset as GameObject)?.gameObject?.GetComponent<IScreen>()?.GetType());
#else
            viewType = new TypeReference(viewAsset?.GetComponent<IView>()?.GetType());
#endif
            controllerType = new TypeReference(viewType.Type.BaseType.GenericTypeArguments[0]);
        }
#endif

        internal void SetType(Type viewType)
        {
            try
            {
                this.viewType = new TypeReference(viewType);
                this.controllerType = new TypeReference(viewType.BaseType.GenericTypeArguments[0]);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}
