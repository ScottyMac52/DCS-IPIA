using System.Security.Cryptography;
using System.Text;

namespace Dcs.Ipia.Domain;

/// <summary>Preserves sparse DCS axis tuning.</summary>
public sealed record AxisFilter(
 double? Deadzone=null,
 double? SaturationX=null,
 double? SaturationY=null,
 IReadOnlyList<double>? Curvature=null,
 bool? Invert=null,
 bool? Slider=null,
 bool? HardwareDetent=null,
 double? HardwareDetentAB=null,
 double? HardwareDetentMax=null);
