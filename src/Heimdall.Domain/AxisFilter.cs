using System.Security.Cryptography;
using System.Text;

namespace Heimdall.Domain
{
    /// <summary>Preserves sparse DCS axis tuning.</summary>
    /// <param name="Deadzone">Optional deadzone tuning value.</param>
    /// <param name="SaturationX">Optional saturation x tuning value.</param>
    /// <param name="SaturationY">Optional saturation y tuning value.</param>
    /// <param name="Curvature">Optional curvature tuning value.</param>
    /// <param name="Invert">Optional invert tuning value.</param>
    /// <param name="Slider">Optional slider tuning value.</param>
    /// <param name="HardwareDetent">Optional hardware detent tuning value.</param>
    /// <param name="HardwareDetentAB">Optional hardware detent ab tuning value.</param>
    /// <param name="HardwareDetentMax">Optional hardware detent max tuning value.</param>
    public sealed record AxisFilter(double? Deadzone = null, double? SaturationX = null, double? SaturationY = null,
                                    IReadOnlyList<double>? Curvature = null, bool? Invert = null, bool? Slider = null,
                                    bool? HardwareDetent = null, double? HardwareDetentAB = null,
                                    double? HardwareDetentMax = null);
}
