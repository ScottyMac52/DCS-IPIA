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

/// <summary>Represents snapshot tests.</summary>
public sealed class SnapshotTests {
 #region BEGIN Public
 /// <summary>full snapshot is deterministic hashed and lossless.</summary>
 [Fact] public void Full_snapshot_is_deterministic_hashed_and_lossless(){var huey=Fixture("UH-1H","Huey",1);var tomcat=Fixture("F-14","Tomcat",2);var first=ConfigurationSnapshotSerializer.Serialize([huey,tomcat]);var second=ConfigurationSnapshotSerializer.Serialize([tomcat,huey]);Assert.Equal(first,second);Assert.Equal(64,first.Sha256.Length);var restored=ConfigurationSnapshotSerializer.Deserialize(first.Content);Assert.Equal(first.Content,ConfigurationSnapshotSerializer.Serialize(restored).Content);Assert.EndsWith("\n",first.Content,StringComparison.Ordinal);}
 /// <summary>snapshot rejects invalid content.</summary>
 [Fact] public void Snapshot_rejects_invalid_content(){Assert.Throws<ArgumentNullException>(()=>ConfigurationSnapshotSerializer.Serialize(null!));Assert.Throws<ArgumentException>(()=>ConfigurationSnapshotSerializer.Deserialize(""));Assert.Throws<JsonException>(()=>ConfigurationSnapshotSerializer.Deserialize("null"));Assert.Throws<JsonException>(()=>ConfigurationSnapshotSerializer.Deserialize("{\"schemaVersion\":99,\"modules\":[]}"));}
 #endregion END Public
 #region BEGIN private helpers
 private static ModuleConfiguration Fixture(string dcs,string name,int seed){var id=new Guid(seed,0,0,new byte[8]);var profile=new DeviceProfile(new Guid(seed+10,0,0,new byte[8]),"tm-mfd",$"MFD{seed}","panel","profile.lua");return new(new(id,dcs,name),[profile],[],[new(profile.Id,BindingSection.Key,"c","Command",BindingDisposition.Added,"JOY_BTN1")],$"repo@{seed}");}
 #endregion END private helpers
}
