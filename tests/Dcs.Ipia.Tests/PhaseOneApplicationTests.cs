using System.Text.Json;
using Dcs.Ipia.Application;
using Dcs.Ipia.Tests.Fakes;
using Dcs.Ipia.Dcs;
using Dcs.Ipia.Domain;
using Dcs.Ipia.Infrastructure;
using Microsoft.Data.Sqlite;
using Xunit;
using Dcs.Ipia.Application.Interfaces;

namespace Dcs.Ipia.Tests;

/// <summary>Represents phase one application tests.</summary>
public sealed class PhaseOneApplicationTests {
 #region BEGIN Public
 /// <summary>service persists only successful imports.</summary>
 /// <returns>A task that completes when the operation finishes</returns>
 [Fact] public async Task Service_persists_only_successful_imports(){var configuration=Fixture();var repository=new MemoryConfigurationRepository();var importer=new StubImporter(new(configuration,[]));var service=new ConfigurationImportService(importer,repository);Assert.True((await service.ImportAsync(Request())).Succeeded);Assert.Equal(configuration,await repository.GetAsync(configuration.Module.Id));importer.Result=new(null,[new("bad","bad",true)]);Assert.False((await service.ImportAsync(Request())).Succeeded);Assert.Equal(1,repository.Writes);await Assert.ThrowsAsync<ArgumentNullException>(()=>service.ImportAsync(null!));}
 #endregion END Public
 #region BEGIN private helpers
 private static ModuleConfiguration Fixture(){var module=new DcsModule(Guid.NewGuid(),"UH-1H","Huey");return new(module,[],[],[],"test");}
 private static ModuleImportRequest Request()=>new(new(Guid.NewGuid(),"UH-1H","Huey"),[],null,"test");
 #endregion END private helpers
}
