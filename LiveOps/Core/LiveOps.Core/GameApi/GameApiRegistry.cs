using System;
using System.Collections.Generic;

namespace LiveOps.GameApi
{
    public sealed class GameApiRegistry
    {
        private readonly Dictionary<Type, HandlerEntry> _byType = new Dictionary<Type, HandlerEntry>();
        private readonly Dictionary<string, Type> _byKey = new Dictionary<string, Type>();

        public GameApiRegistry()
        {
        }

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

        /// <summary>Registers a handler with explicit request/response types. Wire key is <paramref name="requestType"/>.Name.</summary>
        public void Register(Type handlerType, Type requestType, Type responseType)
        {
            if (handlerType == null)
            {
                throw new ArgumentNullException(nameof(handlerType));
            }

            if (requestType == null)
            {
                throw new ArgumentNullException(nameof(requestType));
            }

            if (responseType == null)
            {
                throw new ArgumentNullException(nameof(responseType));
            }

            if (handlerType.IsAbstract || handlerType.IsInterface)
            {
                throw new ArgumentException("Handler type must be a concrete class.", nameof(handlerType));
            }

            string wireKey = requestType.Name;
            var entry = new HandlerEntry
            {
                RequestType = requestType,
                ResponseType = responseType,
                HandlerType = handlerType,
            };
            AddHandlerEntry(requestType, wireKey, entry);
        }

        public void RegisterHandlerType(Type type)
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
                Type requestType = args[0];
                string wireKey = requestType.Name;
                var entry = new HandlerEntry
                {
                    RequestType = requestType,
                    ResponseType = args[1],
                    HandlerType = type,
                };
                AddHandlerEntry(requestType, wireKey, entry);
            }
        }

        private void AddHandlerEntry(Type requestType, string wireKey, HandlerEntry entry)
        {
            if (_byType.TryGetValue(requestType, out HandlerEntry? existing))
            {
                if (existing.HandlerType == entry.HandlerType)
                {
                    return;
                }

                throw new InvalidOperationException($"Duplicate GameApi handler for request type '{requestType.FullName}'.");
            }

            if (_byKey.TryGetValue(wireKey, out Type? otherRequest) && !ReferenceEquals(otherRequest, requestType))
            {
                throw new InvalidOperationException(
                    $"Duplicate GameApi request key '{wireKey}' for types '{otherRequest?.FullName}' and '{requestType.FullName}'.");
            }

            _byType[requestType] = entry;
            _byKey[wireKey] = requestType;
        }

        public bool Contains(string requestKey)
        {
            return !string.IsNullOrEmpty(requestKey) && _byKey.ContainsKey(requestKey) && _byType.ContainsKey(_byKey[requestKey]);
        }

        public bool TryGet(string requestKey, out HandlerEntry? entry)
        {
            if (string.IsNullOrEmpty(requestKey) || !_byKey.TryGetValue(requestKey, out Type? requestType) || requestType == null)
            {
                entry = null;
                return false;
            }

            if (!_byType.TryGetValue(requestType, out HandlerEntry? found))
            {
                entry = null;
                return false;
            }

            entry = found;
            return true;
        }

        public bool TryGet(Type requestType, out HandlerEntry? entry)
        {
            if (requestType == null)
            {
                throw new ArgumentNullException(nameof(requestType));
            }

            if (!_byType.TryGetValue(requestType, out HandlerEntry? found))
            {
                entry = null;
                return false;
            }

            entry = found;
            return true;
        }

        public bool TryResolve(string requestKey, out Type? requestType, out Type? responseType)
        {
            if (!TryGet(requestKey, out HandlerEntry? e) || e == null)
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
