using System;
using System.IO;
using LiveOps.DTO.Json;
using Xunit;

namespace LiveOps.Tests
{
    public sealed class CrossPlatformTypeBinderTests
    {
        [Fact]
        public void BindToType_allows_Game_LiveOps_assembly_name_without_throwing_when_type_missing()
        {
            var binder = new CrossPlatformTypeBinder();
            Type? t = binder.BindToType("Game.LiveOps.Tracks.DTO", "Nonexistent.Type");
            Assert.Null(t);
        }

        [Fact]
        public void BindToType_allows_Scaffold_LiveOps_assembly_name_without_throwing_when_type_missing()
        {
            var binder = new CrossPlatformTypeBinder();
            Type? t = binder.BindToType("Scaffold.LiveOps.Example.DTO", "Nonexistent.Type");
            Assert.Null(t);
        }

        [Fact]
        public void BindToType_disallows_unknown_assembly_name()
        {
            var binder = new CrossPlatformTypeBinder();
            InvalidDataException ex = Assert.Throws<InvalidDataException>(() =>
                binder.BindToType("MaliciousAssembly", "Some.Type"));
            Assert.Contains("MaliciousAssembly", ex.Message);
        }
    }
}
