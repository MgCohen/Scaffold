using System;
using System.Collections.Generic;
using System.Linq;
using GameModuleDTO.ModuleRequests;
using VContainer;

namespace Scaffold.LiveOps
{
    internal sealed class ModuleResponseDispatchService
    {
        public ModuleResponseDispatchService(IObjectResolver objectResolver)
        {
            if (objectResolver == null)
            {
                throw new ArgumentNullException(nameof(objectResolver));
            }

            this.objectResolver = objectResolver;
        }

        private readonly IObjectResolver objectResolver;

        public void DispatchNestedResponses(ModuleResponse root)
        {
            if (root == null || root.Responses == null || root.Responses.Count == 0)
            {
                return;
            }

            IEnumerable<IResponseHandler> handlers = objectResolver.Resolve<IEnumerable<IResponseHandler>>();
            if (handlers == null || !handlers.Any())
            {
                return;
            }

            DispatchChildren(root.Responses, handlers);
        }

        private void DispatchChildren(IReadOnlyList<ModuleResponse> children, IEnumerable<IResponseHandler> handlers)
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

        private void DispatchForNode(ModuleResponse node, IEnumerable<IResponseHandler> handlers)
        {
            Type nodeType = node.GetType();
            foreach (IResponseHandler handler in handlers)
            {
                if (handler != null && handler.HandledResponseType == nodeType)
                {
                    handler.Handle(node);
                }
            }
        }
    }
}
