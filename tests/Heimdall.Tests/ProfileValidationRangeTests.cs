using System.Text.Json;
using Heimdall.Application.Validation;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Checks independent range arithmetic, zero-sized declarations, reference ownership and native boundary cases.</summary>
    public sealed class ProfileValidationRangeTests
    {
        #region BEGIN Public
        /// <summary>All ranges are half-open; empty ranges neither overlap nor own an input.</summary>
        [Theory]
        [InlineData("[{\"o\":0,\"c\":2},{\"o\":2,\"c\":2}]", "[{\"index\":0},{\"index\":3}]", true)]
        [InlineData("[{\"o\":1,\"c\":0},{\"o\":0,\"c\":2}]", "[{\"index\":1}]", true)]
        [InlineData("[{\"o\":0,\"c\":0}]", "[{\"index\":0}]", false)]
        [InlineData("[{\"o\":0,\"c\":2},{\"o\":1,\"c\":2}]", "[{\"index\":1}]", false)]
        [InlineData("[{\"o\":-1,\"c\":1}]", "[]", false)]
        [InlineData("[{\"o\":0,\"c\":-1}]", "[]", false)]
        [InlineData("[{\"o\":0,\"c\":2147483649}]", "[]", false)]
        [InlineData("[{\"o\":2147483647,\"c\":2}]", "[]", false)]
        [InlineData("[{\"o\":2147483647,\"c\":1}]", "[{\"index\":2147483647}]", true)]
        [InlineData("[{\"o\":\"0\",\"c\":1}]", "[]", false)]
        [InlineData("[{\"c\":1}]", "[]", false)]
        [InlineData("[null]", "[]", false)]
        [InlineData("[]", "[{\"index\":-1}]", true)]
        [InlineData("[]", "[{\"index\":-2}]", false)]
        [InlineData("[]", "[{}]", false)]
        [InlineData("[]", "[{\"index\":\"1\"}]", false)]
        public void Range_and_reference_rules_cover_the_native_boundaries(string ranges, string references, bool valid)
        {
            var result = Validate("{\"devices\":" + ranges + ",\"inputs\":" + references + "}", new("/native/devices", "o", "c", "/native/inputs/*"));
            Assert.Equal(valid, result.IsValid);
            if (!valid) { Assert.NotEmpty(result.Issues); Assert.Empty(result.ExportPlan!.Files); }
        }
        /// <summary>Wrong collection shapes and caller-defined sentinel/index fields are not silently accepted.</summary>
        [Fact]
        public void Collection_shape_custom_index_and_sentinel_are_explicit()
        {
            Assert.Contains(Validate("{}", new("/native/devices", "o", "c", "/native/inputs/*")).Issues, issue => issue.Code == "range.collection");
            Assert.True(Validate("{\"devices\":[{\"o\":0,\"c\":1}],\"inputs\":[{\"key\":0}]}", new("/native/devices", "o", "c", "/native/inputs/*", Index: "key", UnboundIndex: null)).IsValid);
            Assert.False(Validate("{\"devices\":[],\"inputs\":[{\"index\":-1}]}", new("/native/devices", "o", "c", "/native/inputs/*", UnboundIndex: null)).IsValid);
            Assert.True(Validate("{\"devices\":[{\"o\":0,\"c\":0}],\"inputs\":[]}", new("/native/devices", "o", "c", "/native/inputs/*")).IsValid);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static ProfileValidationResult Validate(string json, ProfileValidationRange range)
        {
            var (backend, profile) = ProfileValidationFixture.Open("war-thunder");
            return ProfileValidationEngine.Validate(backend, profile, ProfileValidationFixture.Rules() with { Ranges = [range] }, () => JsonSerializer.Deserialize<JsonElement>(json));
        }
        #endregion END private helpers
    }
}
