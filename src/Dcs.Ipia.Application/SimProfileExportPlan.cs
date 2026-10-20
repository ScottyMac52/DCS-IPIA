namespace Dcs.Ipia.Application
{
    /// <summary>An atomic, reviewable native payload with explicit conversion diagnostics.</summary>
    /// <param name="Files">Relative output paths; empty on failure.</param>
    /// <param name="Diagnostics">Actionable errors or warnings.</param>
    public sealed record SimProfileExportPlan(IReadOnlyDictionary<string, string> Files, IReadOnlyList<ImportDiagnostic> Diagnostics)
    {
        #region BEGIN Public
        /// <summary>Gets whether the complete plan passed validation.</summary>
        public bool Succeeded => !Diagnostics.Any(x => x.IsError);
        #endregion END Public
    }
}
