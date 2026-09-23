using Dcs.Ipia.Domain;

namespace Dcs.Ipia.Application;

/// <summary>Represents import diagnostic.</summary>
/// <param name="Code">The code.</param>
/// <param name="Message">The message.</param>
/// <param name="IsError">The is error.</param>
public sealed record ImportDiagnostic(string Code,string Message,bool IsError);
