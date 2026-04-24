using System;
using System.Collections.Generic;
using System.Reflection;
using LiveOps.DTO.GameApi;

namespace LiveOps.GameApi
{
    public sealed class GameApiRegistry
    {
        private readonly Dictionary<string, HandlerEntry> _map = new Dictionary<string, HandlerEntry>();

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
                GameApiKeyAttribute? keyAttr = requestType.GetCustomAttribute<GameApiKeyAttribute>(inherit: true);
                if (keyAttr == null)
                {
                    throw new InvalidOperationException(
                        $"Request type '{requestType.FullName}' must be decorated with [{nameof(GameApiKeyAttribute)}(…)].");
                }

                string key = keyAttr.Key;
                if (_map.TryGetValue(key, out HandlerEntry? existing))
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
                    HandlerType = type
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
