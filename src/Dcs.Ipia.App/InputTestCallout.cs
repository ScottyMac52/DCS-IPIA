namespace DcsIpia.Features.InputTest
{
    /// <summary>Positions one assignment layer slice on the live input diagram.</summary>
    /// <param name="InputKey">Native input represented by the slice.</param>
    /// <param name="Chord">Assignment layer and modifier chord.</param>
    /// <param name="X">Left coordinate.</param>
    /// <param name="Y">Top coordinate.</param>
    /// <param name="Width">Callout width.</param>
    /// <param name="Height">Callout height.</param>
    /// <param name="Caption">Displayed assignment text.</param>
    /// <param name="ColorIndex">Assignment layer color index.</param>
    public sealed record InputTestCallout(
        string InputKey,
        string Chord,
        double X,
        double Y,
        double Width,
        double Height,
        string Caption,
        int ColorIndex);
}
