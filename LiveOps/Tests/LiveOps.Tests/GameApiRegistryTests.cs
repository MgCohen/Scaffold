using System;
using System.Threading.Tasks;
using LiveOps.DTO.GameApi;
using LiveOps.DTO.ModuleRequest;
using LiveOps.GameApi;
using Xunit;

namespace LiveOps.Tests
{
    public sealed class GameApiRegistryTests
    {
        [GameApiKey("m1.one")]
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

        [GameApiKey("m1.dup")]
        public sealed class Dup1Request : ModuleRequest<DupResponse>
        {
        }

        [GameApiKey("m1.dup")]
        public sealed class Dup2Request : ModuleRequest<DupResponse>
        {
        }

        public sealed class DupResponse : ModuleResponse
        {
        }

        public sealed class Dup1Handler : IGameApiHandler<Dup1Request, DupResponse>
        {
            public Task<DupResponse> HandleAsync(GameApiSession session, Dup1Request request) =>
                Task.FromResult(new DupResponse());
        }

        public sealed class Dup2Handler : IGameApiHandler<Dup2Request, DupResponse>
        {
            public Task<DupResponse> HandleAsync(GameApiSession session, Dup2Request request) =>
                Task.FromResult(new DupResponse());
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

        [Fact]
        public void TryGet_unknown_key_returns_false()
        {
            var r = new GameApiRegistry();
            r.RegisterHandlerType(typeof(M1AHandler));
            Assert.False(r.TryGet("absent", out _));
        }

        [Fact]
        public void RegisterHandlerType_maps_GameApiKey_to_handler_entry()
        {
            var r = new GameApiRegistry();
            r.RegisterHandlerType(typeof(M1AHandler));
            Assert.True(r.TryGet("m1.one", out HandlerEntry? e));
            Assert.NotNull(e);
            Assert.Same(typeof(M1ARequest), e!.RequestType);
            Assert.Same(typeof(M1AHandler), e.HandlerType);
        }

        [Fact]
        public void Duplicate_key_from_second_handler_type_throws()
        {
            var r = new GameApiRegistry();
            r.RegisterHandlerType(typeof(Dup1Handler));
            Assert.Throws<InvalidOperationException>(() => r.RegisterHandlerType(typeof(Dup2Handler)));
        }

        [Fact]
        public void Request_without_GameApiKey_throws()
        {
            var r = new GameApiRegistry();
            Assert.Throws<InvalidOperationException>(() => r.RegisterHandlerType(typeof(NoKeyHandler)));
        }
    }
}
