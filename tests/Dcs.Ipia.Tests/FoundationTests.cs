using Dcs.Ipia.Application;
using Dcs.Ipia.Dcs;
using Dcs.Ipia.Domain;
using Dcs.Ipia.Infrastructure;
using Dcs.Ipia.Packaging;
using Dcs.Ipia.Rendering;
using Microsoft.Data.Sqlite;

namespace Dcs.Ipia.Tests;

public sealed class DomainTests {
 [Fact] public void Valid_module_is_normalized()=>Assert.Equal("UH-1H",new Module(Guid.NewGuid()," UH-1H "," Huey ").DcsId);
 [Fact] public void Empty_id_is_rejected()=>Assert.Throws<ArgumentException>(()=>new Module(Guid.Empty,"UH-1H","Huey"));
 [Theory][InlineData("","Huey")][InlineData("UH-1H"," ")] public void Missing_names_are_rejected(string id,string name)=>Assert.Throws<ArgumentException>(()=>new Module(Guid.NewGuid(),id,name));
}

public sealed class ApplicationTests {
 [Fact] public async Task Service_exposes_crud(){var repo=new MemoryRepository();var service=new ModuleService(repo);var saved=await service.SaveAsync(new(null,"UH-1H","Huey"));Assert.Equal(saved,await service.GetAsync(saved.Id));Assert.Single(await service.ListAsync());Assert.True(await service.DeleteAsync(saved.Id));Assert.Null(await service.GetAsync(saved.Id));Assert.False(await service.DeleteAsync(saved.Id));}
 private sealed class MemoryRepository:IModuleRepository {private readonly Dictionary<Guid,Module> values=[];public Task<IReadOnlyList<Module>> ListAsync(CancellationToken ct=default)=>Task.FromResult<IReadOnlyList<Module>>(values.Values.ToArray());public Task<Module?> GetAsync(Guid id,CancellationToken ct=default)=>Task.FromResult(values.GetValueOrDefault(id));public Task UpsertAsync(Module value,CancellationToken ct=default){values[value.Id]=value;return Task.CompletedTask;}public Task<bool> DeleteAsync(Guid id,CancellationToken ct=default)=>Task.FromResult(values.Remove(id));}
}

public sealed class InfrastructureTests:IDisposable {
 private readonly string path=Path.Combine(Path.GetTempPath(),$"dcs-ipia-{Guid.NewGuid():N}.db"); private string Connection=>$"Data Source={path}";
 [Fact] public async Task Migration_and_crud_use_real_sqlite(){var migration=new SqliteMigrationRunner(Connection);await migration.MigrateAsync();await migration.MigrateAsync();var repo=new SqliteModuleRepository(Connection);var id=Guid.NewGuid();Assert.Null(await repo.GetAsync(id));await repo.UpsertAsync(new(id,"UH-1H","Huey"));await repo.UpsertAsync(new(id,"UH-1H","Huey updated"));Assert.Equal("Huey updated",(await repo.GetAsync(id))!.DisplayName);Assert.Single(await repo.ListAsync());Assert.True(await repo.DeleteAsync(id));Assert.False(await repo.DeleteAsync(id));}
 [Fact] public async Task Backup_is_verified_and_readable(){await new SqliteMigrationRunner(Connection).MigrateAsync();var backup=path+".backup";try{new SqliteBackupService(Connection).Backup(backup);await using var db=new SqliteConnection($"Data Source={backup}");await db.OpenAsync();var cmd=db.CreateCommand();cmd.CommandText="SELECT COUNT(*) FROM schema_migrations";Assert.Equal(1L,await cmd.ExecuteScalarAsync());}finally{File.Delete(backup);}}
 [Fact] public void Empty_backup_path_is_rejected()=>Assert.Throws<ArgumentException>(()=>new SqliteBackupService(Connection).Backup(""));
 public void Dispose(){File.Delete(path);GC.SuppressFinalize(this);}
}

public sealed class OutputTests {
 private static readonly ModuleDto Huey=new(Guid.Parse("11111111-1111-1111-1111-111111111111"),"UH-1H","Huey");
 private static readonly ModuleDto Tomcat=new(Guid.Parse("22222222-2222-2222-2222-222222222222"),"F-14","Tomcat | B(U)");
 [Fact] public void Snapshot_is_deterministic_and_round_trips(){var sut=new CanonicalSnapshotSerializer();var first=sut.Serialize([Huey,Tomcat]);Assert.Equal(first,sut.Serialize([Tomcat,Huey]));Assert.Equal([Tomcat,Huey],sut.Deserialize(first));}
 [Fact] public void Snapshot_validates_input(){var sut=new CanonicalSnapshotSerializer();Assert.Throws<ArgumentNullException>(()=>sut.Serialize(null!));Assert.Throws<ArgumentException>(()=>sut.Deserialize(""));}
 [Fact] public void Markdown_is_ordered_and_escaped(){var value=new ModuleMarkdownRenderer().Render([Huey,Tomcat]);Assert.True(value.IndexOf("F-14",StringComparison.Ordinal)<value.IndexOf("UH-1H",StringComparison.Ordinal));Assert.Contains("Tomcat \\| B(U)",value,StringComparison.Ordinal);Assert.Throws<ArgumentNullException>(()=>new ModuleMarkdownRenderer().Render(null!));}
 [Fact] public void Manifest_is_unique_and_ordered(){var value=new PackageManifestBuilder().Build([Huey,Tomcat,Huey]);Assert.Equal("1",value.SchemaVersion);Assert.Equal(["F-14","UH-1H"],value.ModuleIds);Assert.Throws<ArgumentNullException>(()=>new PackageManifestBuilder().Build(null!));}
}
