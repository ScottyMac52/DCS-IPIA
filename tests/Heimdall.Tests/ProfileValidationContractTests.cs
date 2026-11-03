using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Heimdall.Application.Games;
using Heimdall.Application.Validation;
using Heimdall.TestSupport;
using Heimdall.WarThunder;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Checks every adapter's file contract, reference comparison, semantic rejection and atomic session boundary.</summary>
    public sealed class ProfileValidationContractTests
    {
        #region BEGIN Public
        /// <summary>All adapters implement validation through the same interface and preserve loaded data.</summary>
        [Theory]
        [InlineData("dcs")][InlineData("elite-dangerous")][InlineData("star-wars-squadrons")][InlineData("war-thunder")]
        public void Every_adapter_validates_its_packaged_file_contract_without_mutating(string id)
        {
            var (backend, profile) = ProfileValidationFixture.Open(id); var before = JsonSerializer.Serialize(profile);
            var definition = backend.ValidationDefinition; var result = backend.Validate(profile, definition);
            Assert.True(result.IsValid, result.ErrorMessage); Assert.NotNull(result.ExportPlan); Assert.True(result.ExportPlan!.Succeeded);
            Assert.NotEmpty(definition.Rules); Assert.Equal(id, definition.BackendId);
            Assert.Equal(before, JsonSerializer.Serialize(profile));
            var hashes = result.ExportPlan.Files.ToDictionary(file => file.Key, file => Hash(file.Value), StringComparer.Ordinal);
            Assert.True(backend.Validate(profile, definition with { ExpectedFiles = hashes }).IsValid);
            hashes[hashes.Keys.First()] = new string('0', 64);
            var mismatch = backend.Validate(profile, definition with { ExpectedFiles = hashes });
            Assert.False(mismatch.IsValid); Assert.Contains(mismatch.Issues, issue => issue.Code == "reference.file");
            Assert.Empty(mismatch.ExportPlan!.Files); Assert.False(mismatch.ExportPlan.Succeeded);
        }
        /// <summary>Changing the reference definition rejects unchanged native output independently of exporter logic.</summary>
        [Fact]
        public void Explicit_file_rules_can_change_without_adapter_code_changes()
        {
            var (backend, profile) = ProfileValidationFixture.Open("war-thunder");
            var path = Path.Combine(Path.GetTempPath(), "validation-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                var text = """{"schemaVersion":1,"backendId":"war-thunder","backendVersion":1,"profileSchemaVersion":1,"includePortable":true,"rules":[{"path":"/profile/contextId","type":"string","allowedValues":["controls"]}],"ranges":[]}""";
                File.WriteAllText(path, text);
                Assert.True(backend.Validate(profile, ProfileValidationDefinitions.Parse(File.ReadAllText(path))).IsValid);
                File.WriteAllText(path, text.Replace("[\"controls\"]", "[\"different\"]", StringComparison.Ordinal));
                var result = backend.Validate(profile, ProfileValidationDefinitions.Parse(File.ReadAllText(path)));
                var issue = Assert.Single(result.Issues, issue => issue.IsError);
                Assert.Equal("/profile/contextId", issue.Path); Assert.Contains("different", issue.Expected, StringComparison.Ordinal);
                Assert.Contains("controls", issue.Actual, StringComparison.Ordinal); Assert.NotEmpty(result.ErrorMessage);
                Assert.Empty(result.ExportPlan?.Files ?? new Dictionary<string, string>());
            }
            finally { File.Delete(path); }
        }
        /// <summary>Native range validation catches malformed captures even when exact preservation export succeeds.</summary>
        [Theory]
        [InlineData("overlap", "range.overlap")][InlineData("reference", "range.reference")][InlineData("negative", "range.bounds")]
        public void File_range_rules_independently_reject_native_index_errors(string change, string code)
        {
            var backend = new WarThunderProfileBackend(); var source = WarThunderBackendFixture.Source;
            source = change switch
            {
                "overlap" => source.Replace("axesOffset:i=2", "axesOffset:i=1", StringComparison.Ordinal),
                "reference" => source.Replace("joyButton:i=1", "joyButton:i=99", StringComparison.Ordinal),
                _ => source.Replace("axesCount:i=2", "axesCount:i=-1", StringComparison.Ordinal)
            };
            var profile = WarThunderBackendFixture.Import(backend, source);
            Assert.True(backend.Export(profile).Succeeded);
            var result = backend.Validate(profile, backend.ValidationDefinition);
            Assert.False(result.IsValid); Assert.Contains(result.Issues, issue => issue.Code == code && issue.IsError);
            Assert.Empty(result.ExportPlan!.Files);
        }
        /// <summary>Foreign definitions, unsupported versions and missing portable identities fail before native processing.</summary>
        [Fact]
        public void Header_and_required_field_failures_are_structured()
        {
            var (backend, profile) = ProfileValidationFixture.Open("war-thunder"); var definition = backend.ValidationDefinition;
            Assert.Contains(backend.Validate(profile, definition with { BackendId = "dcs" }).Issues, issue => issue.Code == "validation.backend");
            Assert.Contains(backend.Validate(profile, definition with { BackendVersion = 2 }).Issues, issue => issue.Code == "validation.version");
            Assert.Contains(backend.Validate(profile, definition with { SchemaVersion = 99 }).Issues, issue => issue.Code == "definition.invalid");
            profile.NativeData.Clear();
            var missing = backend.Validate(profile, definition);
            Assert.Contains(missing.Issues, issue => issue.Code == "rule.required" && issue.Path.Contains("nativeData", StringComparison.Ordinal));
            Assert.Null(missing.ExportPlan);
            Assert.Throws<OperationCanceledException>(() => backend.Validate(profile, definition, new CancellationToken(true)));
        }
        /// <summary>Native evidence corruption remains an error and unsupported preserved syntax remains an actionable warning.</summary>
        [Fact]
        public void Native_semantic_errors_and_preservation_warnings_are_retained()
        {
            var (backend, profile) = ProfileValidationFixture.Open("war-thunder");
            profile.Metadata["forged"] = "changed";
            Assert.False(backend.Validate(profile, backend.ValidationDefinition).IsValid);
            var opaque = WarThunderBackendFixture.Import(new(), WarThunderBackendFixture.Source.Replace("settings{", "settings{future:q=opaque;", StringComparison.Ordinal));
            var result = backend.Validate(opaque, backend.ValidationDefinition);
            Assert.True(result.IsValid, result.ErrorMessage); Assert.Contains(result.Issues, issue => !issue.IsError && issue.Code.Contains("opaque", StringComparison.Ordinal));
        }
        /// <summary>Known-good comparison is opt-in and failed staged edits cannot replace the current or saved document.</summary>
        [Fact]
        public void Session_uses_the_contract_before_publishing_an_edit_or_export()
        {
            using var issuer = new OwnerLicenseTestIssuer(); var (backend, profile) = ProfileValidationFixture.Open("war-thunder");
            var definition = backend.ValidationDefinition with { ExpectedFiles = backend.Export(profile).Files.ToDictionary(file => file.Key, file => Hash(file.Value)) };
            var session = new GameProfileSession(backend, new(1, Guid.NewGuid(), "Validated", profile, new()), issuer.Policy, [], () => [], validationDefinition: definition);
            Assert.True(session.Validate().IsValid); var before = JsonSerializer.Serialize(session.Snapshot);
            var record = WarThunderBackendFixture.Binding(profile, "ID_FIRE"); var field = WarThunderBackendFixture.Field(record, "joyButton");
            Assert.Throws<InvalidOperationException>(() => session.ApplyNativeEdit(new WarThunderNativeEdit(WarThunderEditKind.Rebind, record.Id,
                field.Address, "war-thunder.joystick.0", NativeIndex: 2)));
            Assert.Equal(before, JsonSerializer.Serialize(session.Snapshot)); Assert.False(session.HasChanges); Assert.True(session.Export().Succeeded);
        }
        /// <summary>Reference file sets reject missing and extra outputs; native failures stay structured and cancellation propagates.</summary>
        [Fact]
        public void Reference_sets_and_native_snapshot_failures_fail_closed()
        {
            var (backend, profile) = ProfileValidationFixture.Open("war-thunder"); var definition = ProfileValidationFixture.Rules();
            var missing = backend.Validate(profile, definition with { ExpectedFiles = new Dictionary<string, string> { ["missing.blk"] = new string('A', 64) } });
            Assert.Contains(missing.Issues, issue => issue.Code == "reference.file" && issue.Actual == "<missing>");
            Assert.Contains(missing.Issues, issue => issue.Code == "reference.unexpected");
            var failure = ProfileValidationEngine.Validate(backend, profile, definition, () => throw new InvalidDataException("Unreadable native snapshot"));
            Assert.Contains(failure.Issues, issue => issue.Code == "validation.native"); Assert.Empty(failure.ExportPlan!.Files);
            Assert.Throws<OperationCanceledException>(() => ProfileValidationEngine.Validate(backend, profile, definition, () => throw new OperationCanceledException()));
            Assert.Throws<ArgumentNullException>(() => ProfileValidationEngine.Validate(null!, profile, definition));
            Assert.Throws<ArgumentNullException>(() => ProfileValidationEngine.Validate(backend, null!, definition));
            Assert.Throws<ArgumentNullException>(() => ProfileValidationEngine.Validate(backend, profile, null!));
            Assert.True(ProfileValidationEngine.Validate(backend, profile, ProfileValidationFixture.Rules(new ProfileValidationRule("/native", "null"))).IsValid);
        }
        /// <summary>Typed command parameters use escaped native identities and need no command-specific validation code.</summary>
        [Fact]
        public void War_Thunder_parameter_rules_read_native_typed_values()
        {
            var (backend, profile) = ProfileValidationFixture.Open("war-thunder");
            var rules = ProfileValidationFixture.Rules(
                new("/native/commands/axes~1pitch/*/parameters/kMul/*/value", "number", Minimum: 1.25, Maximum: 1.25),
                new("/native/commands/axes~1pitch/*/parameters/inverse/*/value", "boolean", AllowedValues: [JsonSerializer.SerializeToElement(false)]),
                new("/native/commands/hotkeys~1ID_FUTURE/*/parameters/future/*/value", "string", AllowedValues: [JsonSerializer.SerializeToElement("retained")]));
            Assert.True(backend.Validate(profile, rules).IsValid);
            var changed = rules with { Rules = [new("/native/commands/axes~1pitch/*/parameters/kMul/*/value", "number", Maximum: 1)] };
            Assert.Contains(backend.Validate(profile, changed).Issues, issue => issue.Code == "rule.maximum");
        }
        /// <summary>Both native boolean encodings expose the same typed value without changing the retained literal.</summary>
        [Theory]
        [InlineData("yes", true)][InlineData("1", true)][InlineData("no", false)][InlineData("0", false)]
        public void Native_boolean_literals_remain_typed_and_preserved(string literal, bool expected)
        {
            var backend = new WarThunderProfileBackend();
            var source = WarThunderBackendFixture.Source.Replace("inverse:b=no", "inverse:b=" + literal, StringComparison.Ordinal);
            var profile = WarThunderBackendFixture.Import(backend, source);
            var definition = ProfileValidationFixture.Rules(new ProfileValidationRule("/native/commands/axes~1pitch/*/parameters/inverse/*/value", "boolean",
                AllowedValues: [JsonSerializer.SerializeToElement(expected)]));
            var result = backend.Validate(profile, definition);
            Assert.True(result.IsValid, result.ErrorMessage); Assert.Equal(source, Assert.Single(result.ExportPlan!.Files).Value);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
        #endregion END private helpers
    }
}
