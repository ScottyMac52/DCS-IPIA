using Dcs.Ipia.Application;

namespace Dcs.Ipia.Application.Interfaces;

/// <summary>Loads validated defaults and saves user-specific appearance and editor preferences.</summary>
public interface IAppSettingsService {
 #region BEGIN Public
 /// <summary>Gets the effective preferences for the current session.</summary>
 AppSettings Current {get;}
 /// <summary>Gets a non-fatal settings load or save diagnostic, if one occurred.</summary>
 string? LastError {get;}
 /// <summary>Applies and attempts to persist a new appearance palette.</summary>
 /// <param name="theme">A supported palette.</param>
 /// <returns>Whether the new value was persisted.</returns>
 bool TryUpdateTheme(AppearanceTheme theme);
 /// <summary>Applies and attempts to persist an occupied-target policy.</summary>
 /// <param name="behavior">A supported policy.</param>
 /// <returns>Whether the new value was persisted.</returns>
 bool TryUpdateOccupiedDestinationBehavior(OccupiedDestinationBehavior behavior);
 #endregion END Public
}
