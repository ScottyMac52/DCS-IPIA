using Dcs.Ipia.Application;
using Dcs.Ipia.Infrastructure;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Dcs.Ipia.Tests;

/// <summary>Checks persisted appearance and drop preferences.</summary>
public sealed class SqliteAppSettingsServiceTests {
 #region BEGIN Public
 /// <summary>Migrates previous user settings once, then reads and writes only SQLite.</summary>
 /// <returns>Completion of the real database test.</returns>
 [Fact] public async Task Preferences_survive_restart_after_legacy_import() {
  var folder=Path.Combine(Path.GetTempPath(),"ipia-pref-"+Guid.NewGuid().ToString("N"));
  Directory.CreateDirectory(folder);
  var database=Path.Combine(folder,"ipia.db");
  var connection="Data Source="+database+";Pooling=False";
  var defaults=Path.Combine(folder,"appsettings.json");
  var legacy=Path.Combine(folder,"user-settings.json");
  var theme=Path.Combine(folder,"theme.txt");
  try {
   File.WriteAllText(defaults,"{\"SchemaVersion\":1,\"Theme\":\"Light\",\"OccupiedDestinationBehavior\":\"Ask\"}");
   File.WriteAllText(legacy,"{\"SchemaVersion\":1,\"Theme\":\"Dark\",\"OccupiedDestinationBehavior\":\"Reject\"}");
   await new SqliteMigrationRunner(connection).MigrateAsync();
   var first=new SqliteAppSettingsService(connection,defaults,legacy,theme);
   Assert.Equal(AppearanceTheme.Dark,first.Current.Theme);
   Assert.Equal(OccupiedDestinationBehavior.Reject,first.Current.OccupiedDestinationBehavior);
   File.Delete(legacy);
   Assert.True(first.TryUpdateOccupiedDestinationBehavior(OccupiedDestinationBehavior.ReplaceInDraft));
   var reopened=new SqliteAppSettingsService(connection,defaults,legacy,theme);
   Assert.Equal(AppearanceTheme.Dark,reopened.Current.Theme);
   Assert.Equal(OccupiedDestinationBehavior.ReplaceInDraft,reopened.Current.OccupiedDestinationBehavior);
   Assert.False(File.Exists(legacy));
   await using var db=new SqliteConnection(connection);await db.OpenAsync();
   var command=db.CreateCommand();command.CommandText="SELECT COUNT(*) FROM app_preferences";
   Assert.Equal(1L,await command.ExecuteScalarAsync());
  }
  finally {Directory.Delete(folder,true);}
 }
 /// <summary>Uses defaults when no previous preference exists and rejects unsupported choices.</summary>
 /// <returns>Completion of the database migration.</returns>
 [Fact] public async Task Defaults_and_invalid_choices_are_handled() {
  var folder=Path.Combine(Path.GetTempPath(),"ipia-pref-default-"+Guid.NewGuid().ToString("N"));
  Directory.CreateDirectory(folder);
  var connection="Data Source="+Path.Combine(folder,"ipia.db")+";Pooling=False";
  try {
   await new SqliteMigrationRunner(connection).MigrateAsync();
   var settings=new SqliteAppSettingsService(connection,Path.Combine(folder,"missing.json"),
    Path.Combine(folder,"missing-user.json"),Path.Combine(folder,"missing-theme.txt"));
   Assert.Equal(AppearanceTheme.Light,settings.Current.Theme);
   Assert.Equal(OccupiedDestinationBehavior.Ask,settings.Current.OccupiedDestinationBehavior);
   Assert.Throws<ArgumentOutOfRangeException>(()=>settings.TryUpdateTheme((AppearanceTheme)99));
   Assert.Throws<ArgumentOutOfRangeException>(()=>settings.TryUpdateOccupiedDestinationBehavior((OccupiedDestinationBehavior)99));
   Assert.True(settings.TryUpdateTheme(AppearanceTheme.Dark));
   Assert.Equal(AppearanceTheme.Dark,new SqliteAppSettingsService(connection,Path.Combine(folder,"missing.json"),
    Path.Combine(folder,"missing-user.json"),Path.Combine(folder,"missing-theme.txt")).Current.Theme);
  }
  finally {Directory.Delete(folder,true);}
 }
 /// <summary>Imports an earlier theme but keeps subsequent database changes authoritative.</summary>
 /// <returns>Completion of the migration check.</returns>
 [Fact] public async Task Legacy_theme_is_migrated_only_once() {
  var folder=Path.Combine(Path.GetTempPath(),"ipia-pref-theme-"+Guid.NewGuid().ToString("N"));
  Directory.CreateDirectory(folder);
  var connection="Data Source="+Path.Combine(folder,"ipia.db")+";Pooling=False";
  var themePath=Path.Combine(folder,"theme.txt");
  try {
   await new SqliteMigrationRunner(connection).MigrateAsync();
   File.WriteAllText(themePath,"Dark");
   var settings=new SqliteAppSettingsService(connection,Path.Combine(folder,"defaults.json"),
    Path.Combine(folder,"user.json"),themePath);
   Assert.Equal(AppearanceTheme.Dark,settings.Current.Theme);
   File.WriteAllText(themePath,"Light");
   Assert.Equal(AppearanceTheme.Dark,new SqliteAppSettingsService(connection,Path.Combine(folder,"defaults.json"),
    Path.Combine(folder,"user.json"),themePath).Current.Theme);
  }
  finally {Directory.Delete(folder,true);}
 }
 /// <summary>Rejects a corrupt persisted preference without writing to old files.</summary>
 /// <returns>Completion of the corrupt-row check.</returns>
 [Fact] public async Task Corrupt_database_preferences_use_defaults() {
  var folder=Path.Combine(Path.GetTempPath(),"ipia-pref-corrupt-"+Guid.NewGuid().ToString("N"));
  Directory.CreateDirectory(folder);
  var connection="Data Source="+Path.Combine(folder,"ipia.db")+";Pooling=False";
  try {
   await new SqliteMigrationRunner(connection).MigrateAsync();
   await using(var db=new SqliteConnection(connection)) {
    await db.OpenAsync();var cmd=db.CreateCommand();
    cmd.CommandText="INSERT INTO app_preferences VALUES(1,1,'Neon','Erase')";
    await cmd.ExecuteNonQueryAsync();
   }
   var settings=new SqliteAppSettingsService(connection,Path.Combine(folder,"defaults.json"),
    Path.Combine(folder,"user.json"),Path.Combine(folder,"theme.txt"));
   Assert.Equal(AppearanceTheme.Light,settings.Current.Theme);
   Assert.NotNull(settings.LastError);
   Assert.True(settings.TryUpdateOccupiedDestinationBehavior(OccupiedDestinationBehavior.Reject));
  }
  finally {Directory.Delete(folder,true);}
 }
 #endregion END Public
}
