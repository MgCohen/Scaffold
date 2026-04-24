using System;
using System.Threading.Tasks;
using LiveOps.DTO.ModuleRequest;
using LiveOps.GameApi;
using Xunit;

namespace LiveOps.Tests
{
    public sealed class GameApiRegistryTests
    {
        public sealed class M1ARequest : ModuleRequest<M1AResponse>
        {
        }

        public sealed class M1AResponse : ModuleResponse
        {
        }

        public sealed class M1AHandler : IGameApiHandler<M1ARequest, M1AResponse>
        {
            public Task<M1AResponse> HandleAsync(GameApiSession session, M1ARequest request) =>
                Task.FromResult(new M1AResponse());
        }

        public sealed class NoKeyRequest : ModuleRequest<NoKeyResponse>
        {
        }

        public sealed class NoKeyResponse : ModuleResponse
        {
        }

        public sealed class NoKeyHandler : IGameApiHandler<NoKeyRequest, NoKeyResponse>
        {
            public Task<NoKeyResponse> HandleAsync(GameApiSession session, NoKeyRequest request) =>
                Task.FromResult(new NoKeyResponse());
        }

        private static class CollideNameNs1
        {
            public sealed class CollideRequest : ModuleRequest<CollideResponse1>
            {
            }

            public sealed class CollideResponse1 : ModuleResponse
            {
            }

            public sealed class CollideHandler1 : IGameApiHandler<CollideRequest, CollideResponse1>
            {
                public Task<CollideResponse1> HandleAsync(GameApiSession session, CollideRequest request) =>
                    Task.FromResult(new CollideResponse1());
            }
        }

        private static class CollideNameNs2
        {
            public sealed class CollideRequest : ModuleRequest<CollideResponse2>
            {
            }

            public sealed class CollideResponse2 : ModuleResponse
            {
            }

            public sealed class CollideHandler2 : IGameApiHandler<CollideRequest, CollideResponse2>
            {
                public Task<CollideResponse2> HandleAsync(GameApiSession session, CollideRequest request) =>
                    Task.FromResult(new CollideResponse2());
            }
        }

        [Fact]
        public void TryGet_unknown_key_returns_false()
        {
            var r = new GameApiRegistry();
            r.RegisterHandlerType(typeof(M1AHandler));
            Assert.False(r.TryGet("absent", out _));
        }

        [Fact]
        public void RegisterHandlerType_keys_by_TypeName_default()
        {
            var r = new GameApiRegistry();
            r.RegisterHandlerType(typeof(M1AHandler));
            Assert.True(r.TryGet("M1ARequest", out HandlerEntry? e));
            Assert.NotNull(e);
            Assert.Same(typeof(M1ARequest), e!.RequestType);
            Assert.Same(typeof(M1AHandler), e.HandlerType);
        }

        [Fact]
        public void RegisterHandlerType_indexes_by_RequestType()
        {
            var r = new GameApiRegistry();
            r.RegisterHandlerType(typeof(M1AHandler));
            Assert.True(r.TryGet(typeof(M1ARequest), out HandlerEntry? e));
            Assert.NotNull(e);
            Assert.Same(typeof(M1ARequest), e!.RequestType);
            Assert.Same(typeof(M1AHandler), e.HandlerType);
        }

        [Fact]
        public void RegisterHandlerType_throws_on_duplicate_wire_key_for_same_type_name_different_types()
        {
            var r = new GameApiRegistry();
            r.RegisterHandlerType(typeof(CollideNameNs1.CollideHandler1));
            Assert.Throws<InvalidOperationException>(() => r.RegisterHandlerType(typeof(CollideNameNs2.CollideHandler2)));
        }

        [Fact]
        public void Request_without_attribute_uses_Type_Name()
        {
            var r = new GameApiRegistry();
            r.RegisterHandlerType(typeof(NoKeyHandler));
            Assert.True(r.TryGet("NoKeyRequest", out HandlerEntry? e));
            Assert.NotNull(e);
            Assert.Same(typeof(NoKeyRequest), e!.RequestType);
        }
    }
}
