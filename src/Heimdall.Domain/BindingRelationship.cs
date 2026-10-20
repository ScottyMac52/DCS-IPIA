namespace Heimdall.Domain
{
    /// <summary>Represents binding relationship.</summary>
    /// <param name="Id">The identifier.</param>
    /// <param name="ProfileId">The profile identifier.</param>
    /// <param name="ControlId">The control identifier.</param>
    /// <param name="CommandId">The command identifier.</param>
    /// <param name="Disposition">The disposition.</param>
    /// <param name="InputKey">The input key.</param>
    /// <param name="ModifierChord">The modifier chord.</param>
    /// <param name="AxisTuning">The axis tuning.</param>
    public sealed record BindingRelationship(Guid Id, Guid ProfileId, Guid ControlId, Guid CommandId,
                                             BindingDisposition Disposition, string InputKey,
                                             IReadOnlyList<string> ModifierChord, AxisFilter? AxisTuning);
}
