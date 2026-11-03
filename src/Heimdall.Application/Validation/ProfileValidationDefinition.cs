namespace Heimdall.Application.Validation
{
    /// <summary>A versioned file-backed rule contract; expected file hashes are optional exact-reference checks.</summary>
    public sealed record ProfileValidationDefinition(int SchemaVersion, string BackendId, int BackendVersion,
        int ProfileSchemaVersion, IReadOnlyList<ProfileValidationRule> Rules, IReadOnlyList<ProfileValidationRange> Ranges,
        IReadOnlyDictionary<string, string>? ExpectedFiles = null, bool IncludePortable = false);
}
