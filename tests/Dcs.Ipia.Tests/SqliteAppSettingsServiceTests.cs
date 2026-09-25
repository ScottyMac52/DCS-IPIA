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
 #endregion END Public
}
