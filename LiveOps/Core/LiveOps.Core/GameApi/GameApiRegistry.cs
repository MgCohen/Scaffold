using System;
using System.Collections.Generic;
using System.Reflection;

namespace LiveOps.Core.GameApi
{
    /// <summary>
    /// Maps <c>RequestKey</c> strings to handler metadata for all registered handlers.
    /// </summary>
    public sealed class HandlerEntry
    {
        public Type RequestType { get; set; } = null!;

        public Type ResponseType { get; set; } = null!;

        public Type HandlerType { get; set; } = null!;
    }

    /// <summary>
    /// Maps <c>RequestKey</c> strings to request/response CLR types and concrete handler types.
    /// </summary>
    public sealed class GameApiRegistry
    {
        private readonly Dictionary<string, HandlerEntry> _map = new Dictionary<string, HandlerEntry>();

        public GameApiRegistry(params Assembly[] assemblies)
        {
            if (assemblies == null || assemblies.Length == 0)
            {
                throw new ArgumentException("At least one assembly is required.", nameof(assemblies));
            }

            foreach (Assembly assembly in assemblies)
            {
                foreach (Type type in assembly.GetTypes())
                {
                    if (type.IsAbstract || type.IsInterface)
                    {
                        continue;
                    }

                    RegisterHandlerType(type);
                }
            }
        }

        /// <summary>
        /// Registers a concrete handler type (same logic as assembly scan). Use with explicit registration in <see cref="ModuleConfig"/>.
        /// </summary>
        public void Register(Type handlerType)
        {
            if (handlerType == null)
            {
                throw new ArgumentNullException(nameof(handlerType));
            }

            if (handlerType.IsAbstract || handlerType.IsInterface)
            {
                throw new ArgumentException("Handler type must be a concrete class.", nameof(handlerType));
            }

            RegisterHandlerType(handlerType);
        }

        private void RegisterHandlerType(Type type)
        {
            foreach (Type iface in type.GetInterfaces())
            {
                if (!iface.IsGenericType)
                {
                    continue;
                }

                if (iface.GetGenericTypeDefinition() != typeof(IGameApiHandler<,>))
                {
                    continue;
                }

                Type[] args = iface.GetGenericArguments();
                string key = args[0].Name;
                if (_map.TryGetValue(key, out HandlerEntry existing))
                {
                    if (existing.HandlerType == type)
                    {
                        continue;
                    }

                    throw new InvalidOperationException($"Duplicate GameApi handler for request key '{key}'.");
                }

                _map[key] = new HandlerEntry
                {
                    RequestType = args[0],
                    ResponseType = args[1],
                    HandlerType = type,
                };
            }
        }

        public bool Contains(string requestKey)
        {
            return !string.IsNullOrEmpty(requestKey) && _map.ContainsKey(requestKey);
        }

        public bool TryGet(string requestKey, out HandlerEntry? entry)
        {
            if (string.IsNullOrEmpty(requestKey) || !_map.TryGetValue(requestKey, out HandlerEntry? found))
            {
                entry = null;
                return false;
            }

            entry = found;
            return true;
        }

        public bool TryResolve(string requestKey, out Type requestType, out Type responseType)
        {
            if (!TryGet(requestKey, out HandlerEntry e))
            {
                requestType = null;
                responseType = null;
                return false;
            }

            requestType = e.RequestType;
            responseType = e.ResponseType;
            return true;
        }
    }
}
