using System.Security.Cryptography;
using System.Text;

namespace Dcs.Ipia.Domain;

/// <summary>Lists binding disposition values.</summary>
public enum BindingDisposition {
 /// <summary>Added value.</summary>
 Added,
 /// <summary>Changed value.</summary>
 Changed,
 /// <summary>Removed value.</summary>
 Removed
}
