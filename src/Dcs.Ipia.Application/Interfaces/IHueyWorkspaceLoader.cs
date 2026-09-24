namespace Dcs.Ipia.Application.Interfaces;

/// <summary>Loads committed Huey and DCS-Common source text for a read-only workspace preview.</summary>
public interface IHueyWorkspaceLoader {
 #region BEGIN Public
 /// <summary>Imports the selected repository heads without writing to either checkout.</summary>
 /// <param name="commonRoot">The root of a local DCS-Common Git checkout.</param>
 /// <param name="hueyRoot">The root of a local DCS-UH-1H-Huey Git checkout.</param>
 /// <param name="cancellationToken">Cancellation token for Git reads.</param>
 /// <returns>Validated Huey source data and provenance.</returns>
 Task<HueyWorkspacePreview> LoadAsync(string commonRoot,string hueyRoot,CancellationToken cancellationToken=default);
 #endregion END Public
}
