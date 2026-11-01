using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Heimdall.Application.Interfaces;
using Heimdall.Domain.MultiSim;

namespace Heimdall.Application.Validation
{
    /// <summary>Runs one versioned file-backed contract over loaded profiles, native evidence and optional known-good file hashes.</summary>
    public static class ProfileValidationEngine
    {
        private static readonly JsonSerializerOptions SnapshotOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        #region BEGIN Public
        /// <summary>Combines existing adapter semantic checks with independent shared rules; cancellation propagates and no files are written.</summary>
        public static ProfileValidationResult Validate(ISimProfileBackend backend, ProfileDocument profile, ProfileValidationDefinition definition,
            Func<JsonElement>? nativeSnapshot = null, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(backend); ArgumentNullException.ThrowIfNull(profile); ArgumentNullException.ThrowIfNull(definition);
            cancellationToken.ThrowIfCancellationRequested();
            var issues = new List<ProfileValidationIssue>();
            var error = ProfileValidationDefinitions.Check(definition);
            if (error is not null) return new([new("definition.invalid", "/definition", "supported versioned definition", "invalid", error)]);
            Header(backend, profile, definition, issues);
            if (issues.Count > 0) return new(issues);
            SimProfileExportPlan? plan = null;
            try
            {
                var portableRoot = JsonSerializer.SerializeToElement(new { profile }, SnapshotOptions);
                foreach (var rule in definition.Rules.Where(PortableRule))
                { cancellationToken.ThrowIfCancellationRequested(); ProfileRuleValidator.Validate(portableRoot, rule, issues); }
                if (issues.Any(issue => issue.IsError)) return Finish(issues, null);
                plan = backend.Export(profile, cancellationToken);
                issues.AddRange(plan.Diagnostics.Select(issue => new ProfileValidationIssue(issue.Code, "/native", "native adapter contract", "see message", issue.Message, issue.IsError)));
                if (issues.Any(issue => issue.IsError)) return Finish(issues, plan);
                var root = JsonSerializer.SerializeToElement(new
                {
                    profile, native = nativeSnapshot?.Invoke(),
                    catalog = backend.GetCatalog(profile).Commands.Select(command => new { command.Id, command.Type, command.Context, command.Metadata }),
                    files = plan.Files.Keys.Order(StringComparer.Ordinal).ToArray()
                }, SnapshotOptions);
                foreach (var rule in definition.Rules.Where(rule => !PortableRule(rule))) { cancellationToken.ThrowIfCancellationRequested(); ProfileRuleValidator.Validate(root, rule, issues); }
                foreach (var rule in definition.Ranges) { cancellationToken.ThrowIfCancellationRequested(); ProfileRangeValidator.Validate(root, rule, issues); }
                ReferenceFiles(definition, plan, issues);
            }
            catch (Exception failure) when (failure is InvalidDataException or InvalidOperationException or ArgumentException or JsonException or FormatException or OverflowException)
            { issues.Add(new("validation.native", "/native", "readable adapter evidence", failure.GetType().Name, failure.Message)); }
            cancellationToken.ThrowIfCancellationRequested();
            return Finish(issues, plan);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static bool PortableRule(ProfileValidationRule rule) => rule.Path.StartsWith("/profile/", StringComparison.Ordinal) && rule.References is null;
        private static ProfileValidationResult Finish(List<ProfileValidationIssue> issues, SimProfileExportPlan? plan) =>
            new(issues.AsReadOnly(), plan is not null && issues.Any(issue => issue.IsError)
                ? new(new Dictionary<string, string>(), issues.Select(issue => new ImportDiagnostic(issue.Code, issue.Path + ": " + issue.Message, issue.IsError)).ToArray()) : plan);
        private static void Header(ISimProfileBackend backend, ProfileDocument profile, ProfileValidationDefinition definition, List<ProfileValidationIssue> issues)
        {
            if (definition.BackendId != backend.SimId || profile.BackendId != backend.SimId)
                issues.Add(new("validation.backend", "/profile/backendId", backend.SimId, "profile=" + profile.BackendId + "; definition=" + definition.BackendId, "Profile and definition must belong to the active adapter."));
            if (profile.SchemaVersion != definition.ProfileSchemaVersion || profile.BackendVersion != definition.BackendVersion)
                issues.Add(new("validation.version", "/profile", definition.ProfileSchemaVersion + "/" + definition.BackendVersion,
                    profile.SchemaVersion + "/" + profile.BackendVersion, "Profile versions do not match the validation contract."));
        }
        private static void ReferenceFiles(ProfileValidationDefinition definition, SimProfileExportPlan plan, List<ProfileValidationIssue> issues)
        {
            if (definition.ExpectedFiles is null) return;
            foreach (var expected in definition.ExpectedFiles)
            {
                var actual = plan.Files.TryGetValue(expected.Key, out var text) ? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))) : "<missing>";
                if (!string.Equals(actual, expected.Value, StringComparison.OrdinalIgnoreCase))
                    issues.Add(new("reference.file", "/files/" + ValidationPaths.Escape(expected.Key), expected.Value, actual, "Native output differs from the explicit known-good reference."));
            }
            foreach (var file in plan.Files.Keys.Where(file => !definition.ExpectedFiles.ContainsKey(file)))
                issues.Add(new("reference.unexpected", "/files/" + ValidationPaths.Escape(file), "file in reference set", "unexpected", "Native output contains an unexpected reference file."));
        }
        #endregion END private helpers
    }
}
