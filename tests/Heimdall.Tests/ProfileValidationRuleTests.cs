using System.Text.Json;
using Heimdall.Application.Validation;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Exercises shared rule branches with independently authored native snapshots and file contracts.</summary>
    public sealed class ProfileValidationRuleTests
    {
        #region BEGIN Public
        /// <summary>Every supported primitive/container type has positive and negative cases.</summary>
        [Theory]
        [InlineData("string", "\"text\"", true)][InlineData("string", "7", false)]
        [InlineData("integer", "3", true)][InlineData("integer", "3.25", false)]
        [InlineData("number", "2.5", true)][InlineData("number", "1e1000", false)]
        [InlineData("number", "\"2\"", false)][InlineData("boolean", "true", true)]
        [InlineData("boolean", "false", true)][InlineData("boolean", "0", false)]
        [InlineData("object", "{}", true)][InlineData("object", "[]", false)]
        [InlineData("array", "[]", true)][InlineData("array", "{}", false)]
        [InlineData("null", "null", true)][InlineData("null", "false", false)]
        public void Value_types_follow_the_file_rule(string type, string value, bool valid)
        {
            var result = Validate("{\"value\":" + value + "}", new("/native/value", type));
            Assert.Equal(valid, result.IsValid);
            if (!valid) { Assert.Contains(result.Issues, issue => issue.Code == "rule.type"); Assert.Empty(result.ExportPlan!.Files); }
        }
        /// <summary>Bounds are inclusive and independently applied to numbers, strings, arrays and objects.</summary>
        [Theory]
        [InlineData("integer", "1", true)][InlineData("integer", "3", true)][InlineData("integer", "0", false)][InlineData("integer", "4", false)]
        [InlineData("string", "\"a\"", true)][InlineData("string", "\"abc\"", true)][InlineData("string", "\"\"", false)][InlineData("string", "\"abcd\"", false)]
        [InlineData("array", "[1]", true)][InlineData("array", "[]", false)][InlineData("array", "[1,2,3,4]", false)]
        [InlineData("object", "{\"a\":1}", true)][InlineData("object", "{}", false)]
        public void Numeric_and_length_bounds_are_enforced(string type, string value, bool valid)
        {
            var rule = type == "integer" ? new ProfileValidationRule("/native/value", type, Minimum: 1, Maximum: 3)
                : new("/native/value", type, MinimumLength: 1, MaximumLength: 3);
            Assert.Equal(valid, Validate("{\"value\":" + value + "}", rule).IsValid);
        }
        /// <summary>Wildcard expansion retains concrete paths, handles optional/empty collections and supports escaped object keys and indices.</summary>
        [Fact]
        public void Path_selection_required_and_optional_values_are_precise()
        {
            Assert.True(Validate("{}", new("/native/absent", "string", Required: false)).IsValid);
            Assert.Contains(Validate("{}", new("/native/absent/value", "string")).Issues, issue => issue.Path == "/native/absent/value" && issue.Code == "rule.required");
            Assert.True(Validate("{\"items\":[]}", new("/native/items/*/id", "string")).IsValid);
            Assert.True(Validate("{\"items\":[{\"id\":\"one\"}]}", new("/native/items/0/id", "string")).IsValid);
            Assert.False(Validate("{\"items\":[{}]}", new("/native/items/*/id", "string")).IsValid);
            Assert.False(Validate("{\"items\":4}", new("/native/items/*/id", "string")).IsValid);
            Assert.False(Validate("{\"items\":[]}", new("/native/items/4/id", "string")).IsValid);
            Assert.True(Validate("{\"a/b~\":\"ok\"}", new("/native/a~1b~0", "string")).IsValid);
            Assert.True(Validate("{\"items\":{\"x\":\"yes\",\"y\":\"no\"}}", new("/native/items/*", "string")).IsValid);
        }
        /// <summary>Allowed values compare decoded strings and numeric values; unique keys and references have separate diagnostics.</summary>
        [Fact]
        public void Allowed_unique_and_reference_rules_do_not_flatten_identity()
        {
            Assert.True(Validate("{\"value\":\"a\"}", new("/native/value", "string", AllowedValues: [JsonSerializer.SerializeToElement("a")])).IsValid);
            Assert.False(Validate("{\"value\":\"b\"}", new("/native/value", "string", AllowedValues: [JsonSerializer.SerializeToElement("a")])).IsValid);
            Assert.True(Validate("{\"value\":1}", new("/native/value", "integer", AllowedValues: [JsonSerializer.SerializeToElement(1.0m)])).IsValid);
            Assert.True(Validate("{\"value\":null}", new("/native/value", "null", AllowedValues: [JsonSerializer.SerializeToElement<object?>(null)])).IsValid);
            var unique = new ProfileValidationRule("/native/items", "array", UniqueBy: "id");
            Assert.True(Validate("{\"items\":[{\"id\":\"a\"},{\"id\":\"b\"}]}", unique).IsValid);
            Assert.Contains(Validate("{\"items\":[{\"id\":\"a\"},{\"id\":\"a\"},{}]}", unique).Issues, issue => issue.Code == "rule.unique");
            Assert.False(Validate("{\"items\":[1]}", unique).IsValid);
            var reference = new ProfileValidationRule("/native/value", "string", References: "/native/items/*/id");
            Assert.True(Validate("{\"value\":\"a\",\"items\":[{\"id\":\"a\"}]}", reference).IsValid);
            Assert.Contains(Validate("{\"value\":\"a\",\"items\":[{}]}", reference).Issues, issue => issue.Code == "rule.reference");
        }
        /// <summary>Oversized selection fails closed without a partial native output plan.</summary>
        [Fact]
        public void Wildcard_selection_has_a_bounded_workset()
        {
            var json = JsonSerializer.Serialize(new { items = Enumerable.Repeat(0, 100001) });
            var result = Validate(json, new("/native/items/*", "integer"));
            Assert.False(result.IsValid); Assert.Contains(result.Issues, issue => issue.Code == "validation.native"); Assert.Empty(result.ExportPlan!.Files);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static ProfileValidationResult Validate(string nativeJson, params ProfileValidationRule[] rules)
        {
            var (backend, profile) = ProfileValidationFixture.Open("war-thunder");
            var native = JsonSerializer.Deserialize<JsonElement>(nativeJson);
            return ProfileValidationEngine.Validate(backend, profile, ProfileValidationFixture.Rules(rules), () => native);
        }
        #endregion END private helpers
    }
}
