using System;
using System.Collections.Generic;
using System.Linq;
using LiveOps.DTO.ModuleRequest;
using Scaffold.AppFlow;

namespace Scaffold.LiveOps
{
    internal sealed class ModuleResponseDispatchService
    {
        public ModuleResponseDispatchService(ILayerResolver layerResolver)
        {
            if (layerResolver == null)
            {
                throw new ArgumentNullException(nameof(layerResolver));
            }

            this.layerResolver = layerResolver;
        }

        private readonly ILayerResolver layerResolver;
        private IResponseHandler[] cachedHandlers;

        public void DispatchNestedResponses(ModuleResponse root)
        {
            if (root == null || root.Responses == null || root.Responses.Count == 0)
            {
                return;
            }

            IResponseHandler[] handlers = cachedHandlers ??= ResolveHandlers();
            if (handlers.Length == 0)
            {
                return;
            }

            DispatchChildren(root.Responses, handlers);
        }

        private IResponseHandler[] ResolveHandlers()
        {
            if (!layerResolver.TryResolve(out IEnumerable<IResponseHandler> all))
            {
                return Array.Empty<IResponseHandler>();
            }

            return all?.ToArray() ?? Array.Empty<IResponseHandler>();
        }

        private void DispatchChildren(IReadOnlyList<ModuleResponse> children, IReadOnlyList<IResponseHandler> handlers)
        {
            for (int i = 0; i < children.Count; i++)
            {
                ModuleResponse node = children[i];
                if (node == null)
                {
                    continue;
                }

                DispatchForNode(node, handlers);
            }
        }

        private void DispatchForNode(ModuleResponse node, IReadOnlyList<IResponseHandler> handlers)
        {
            Type nodeType = node.GetType();
            for (int i = 0; i < handlers.Count; i++)
            {
                IResponseHandler handler = handlers[i];
                if (handler != null && handler.HandledResponseType == nodeType)
                {
                    handler.Handle(node);
                }
            }
        }
    }
}
