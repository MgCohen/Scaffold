using LiveOps.DTO.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace LiveOps.Tests
{
    public sealed class JsonExtensionsRoundTripTests
    {
        [Fact]
        public void FromJson_resolves_mscorlib_type_names_via_CrossPlatformTypeBinder()
        {
            var model = new Sample { Value = 7 };
            string json = JsonConvert.SerializeObject(
                model,
                new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.All,
                    SerializationBinder = new CrossPlatformTypeBinder()
                });

            // Simulate a Unity client serializing the same $type with mscorlib instead of System.Private.CoreLib
            JToken node = JToken.Parse(json);
            if (node is not JObject root || !root.TryGetValue("$type", out JToken? t))
            {
                Assert.Fail("Expected $type in JSON with TypeNameHandling.All");
                return;
            }

            string typeStr = t.Value<string>() ?? string.Empty;
            typeStr = typeStr.Replace("System.Private.CoreLib", "mscorlib", System.StringComparison.Ordinal);
            root["$type"] = typeStr;

            string roundTrip = root.ToString(Formatting.None);
            Sample? back = roundTrip.FromJson<Sample>();
            Assert.NotNull(back);
            Assert.Equal(7, back.Value);
        }

        private sealed class Sample
        {
            public int Value { get; set; }
        }
    }
}
