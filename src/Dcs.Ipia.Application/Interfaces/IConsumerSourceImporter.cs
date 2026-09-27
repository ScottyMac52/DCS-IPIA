namespace Dcs.Ipia.Application.Interfaces;

/// <summary>Imports pinned, module-owned Huey source files without modifying their repository.</summary>
public interface IConsumerSourceImporter {
 #region BEGIN Public
 /// <summary>Validates and normalizes the supplied aircraft sources.</summary>
 /// <param name="request">Pinned Huey sources and imported shared authority.</param>
 /// <returns>The complete read-only module view or path-specific diagnostics.</returns>
 ConsumerImportResult Import(ConsumerImportRequest request);
 #endregion END Public
}
