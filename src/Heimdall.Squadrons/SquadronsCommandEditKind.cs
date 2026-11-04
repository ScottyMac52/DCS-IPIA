namespace Heimdall.Squadrons
{
    /// <summary>Distinguishes whole-control authoring from explicit native-record editing and axis reversal.</summary>
    public enum SquadronsCommandEditKind
    {
        AssignControl,
        AssignRecord,
        ReverseAxis
    }
}
