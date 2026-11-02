using System.Text.Json;
using Heimdall.Application.Validation;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Rejects unrecognized, malformed or unbounded reference definitions before validation begins.</summary>
    public sealed class ProfileValidationDefinitionTests
    {
        #region BEGIN Public
        /// <summary>Definition JSON is versioned and unknown properties, malformed paths and missing collections fail closed.</summary>
        [Theory]
        [InlineData("")][InlineData("null")][InlineData("{")][InlineData("{}")]
        [InlineData("{\"schemaVersion\":1,\"unknown\":true}")]
        public void Malformed_definition_files_are_rejected(string text) => Assert.Throws<InvalidDataException>(() => ProfileValidationDefinitions.Parse(text));
        /// <summary>Direct DTO callers cannot bypass the same loader constraints.</summary>
        [Theory]
        [InlineData("version")][InlineData("backend")][InlineData("backend-version")][InlineData("profile-version")]
        [InlineData("rules-null")][InlineData("ranges-null")][InlineData("rules-many")][InlineData("ranges-many")]
        [InlineData("path")][InlineData("escape")][InlineData("deep")][InlineData("type")][InlineData("rule-null")]
        [InlineData("minmax")][InlineData("nonfinite")][InlineData("length")][InlineData("max-length")][InlineData("min-max-length")]
        [InlineData("numeric-type")][InlineData("length-type")][InlineData("unique-type")][InlineData("unique-blank")][InlineData("reference-path")]
        [InlineData("allowed-object")][InlineData("allowed-many")][InlineData("allowed-nonfinite")]
        [InlineData("range-null")][InlineData("range-path")][InlineData("range-ref")][InlineData("range-offset")][InlineData("range-count")][InlineData("range-index")][InlineData("range-end")]
        [InlineData("hash-null")][InlineData("hash-key")][InlineData("hash-size")][InlineData("hash-hex")][InlineData("hash-many")][InlineData("unresolved-include")]
        public void Invalid_declarative_rules_cannot_reach_native_export(string failure)
        {
            var definition = Invalid(failure); var (backend, profile) = ProfileValidationFixture.Open("war-thunder");
            var result = backend.Validate(profile, definition);
            Assert.False(result.IsValid); Assert.Equal("definition.invalid", Assert.Single(result.Issues).Code); Assert.Null(result.ExportPlan);
        }
        /// <summary>Parsed collections are frozen and large files are rejected without reading a game directory.</summary>
        [Fact]
        public void Definition_loading_is_bounded_and_read_only()
        {
            Assert.Throws<ArgumentNullException>(() => ProfileValidationDefinitions.Parse(null!));
            Assert.Throws<InvalidDataException>(() => ProfileValidationDefinitions.Parse(new string(' ', 4 * 1024 * 1024 + 1)));
            Assert.Throws<InvalidDataException>(() => ProfileValidationDefinitions.BuiltIn("../other"));
            var definition = ProfileValidationDefinitions.Parse("""{"schemaVersion":1,"backendId":"war-thunder","backendVersion":1,"profileSchemaVersion":1,"rules":[{"path":"/profile/contextId","type":"string","allowedValues":["controls"]}],"ranges":[]}""");
            Assert.Throws<NotSupportedException>(() => ((IList<ProfileValidationRule>)definition.Rules).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<JsonElement>)definition.Rules[0].AllowedValues!).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<ProfileValidationRange>)definition.Ranges).Clear());
        }
        #endregion END Public
        #region BEGIN private helpers
        private static ProfileValidationDefinition Invalid(string failure)
        {
            var definition = ProfileValidationFixture.Rules();
            var rule = new ProfileValidationRule("/native/value", "integer"); var range = new ProfileValidationRange("/native/devices", "o", "c", "/native/inputs/*");
            return failure switch
            {
                "version" => definition with { SchemaVersion = 2 }, "backend" => definition with { BackendId = "" },
                "backend-version" => definition with { BackendVersion = 0 }, "profile-version" => definition with { ProfileSchemaVersion = 0 },
                "rules-null" => definition with { Rules = null! }, "ranges-null" => definition with { Ranges = null! },
                "rules-many" => definition with { Rules = Enumerable.Repeat(rule, 257).ToArray() },
                "ranges-many" => definition with { Ranges = Enumerable.Repeat(range, 33).ToArray() },
                "range-null" => definition with { Ranges = [null!] },
                "range-path" => definition with { Ranges = [range with { Path = "missing" }] },
                "range-ref" => definition with { Ranges = [range with { References = "missing" }] },
                "range-offset" => definition with { Ranges = [range with { Offset = "" }] },
                "range-count" => definition with { Ranges = [range with { Count = "" }] },
                "range-index" => definition with { Ranges = [range with { Index = "" }] },
                "range-end" => definition with { Ranges = [range with { MaximumEnd = 0 }] },
                "hash-many" => definition with { ExpectedFiles = Enumerable.Range(0, 257).ToDictionary(index => index.ToString(System.Globalization.CultureInfo.InvariantCulture), _ => new string('A', 64)) },
                "hash-null" => definition with { ExpectedFiles = new Dictionary<string, string> { ["file"] = null! } },
                "hash-key" => definition with { ExpectedFiles = new Dictionary<string, string> { [""] = new string('A', 64) } },
                "hash-size" => definition with { ExpectedFiles = new Dictionary<string, string> { ["file"] = "A" } },
                "hash-hex" => definition with { ExpectedFiles = new Dictionary<string, string> { ["file"] = new string('Z', 64) } },
                "unresolved-include" => definition with { IncludePortable = true },
                _ => definition with { Rules = [InvalidRule(failure, rule)] }
            };
        }
        private static ProfileValidationRule InvalidRule(string failure, ProfileValidationRule rule) => failure switch
        {
            "path" => rule with { Path = "missing" }, "escape" => rule with { Path = "/a~2b" }, "deep" => rule with { Path = string.Concat(Enumerable.Repeat("/a", 17)) },
            "type" => rule with { Type = "unknown" }, "rule-null" => null!, "minmax" => rule with { Minimum = 2, Maximum = 1 },
            "nonfinite" => rule with { Maximum = double.PositiveInfinity }, "length" => rule with { Type = "string", MinimumLength = -1 },
            "max-length" => rule with { Type = "string", MaximumLength = -1 }, "min-max-length" => rule with { Type = "string", MinimumLength = 2, MaximumLength = 1 },
            "numeric-type" => rule with { Type = "string", Minimum = 1 }, "length-type" => rule with { MinimumLength = 1 },
            "unique-type" => rule with { UniqueBy = "id" }, "unique-blank" => rule with { Type = "array", UniqueBy = "" },
            "reference-path" => rule with { References = "missing" },
            "allowed-object" => rule with { AllowedValues = [JsonSerializer.SerializeToElement(new { value = 1 })] },
            "allowed-many" => rule with { AllowedValues = Enumerable.Repeat(JsonSerializer.SerializeToElement(1), 65).ToArray() },
            _ => rule with { AllowedValues = [JsonSerializer.Deserialize<JsonElement>("1e1000")] }
        };
        #endregion END private helpers
    }
}
