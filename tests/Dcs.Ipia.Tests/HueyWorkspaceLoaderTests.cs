using System.Diagnostics;
using Dcs.Ipia.Dcs;
using Xunit;

namespace Dcs.Ipia.Tests;

/// <summary>Exercises the desktop loader against real temporary Git checkouts.</summary>
public sealed class HueyWorkspaceLoaderTests {
 #region BEGIN Public
 /// <summary>Only committed source is imported even when a checkout has uncommitted changes.</summary>
 [Fact] public async Task Loads_committed_huey_and_common_sources() {
  var temp=Path.Combine(Path.GetTempPath(),"DcsIpiaWorkspace-"+Guid.NewGuid().ToString("N"));
  var common=Path.Combine(temp,"common");var huey=Path.Combine(temp,"huey");
  try {
   Directory.CreateDirectory(common);Directory.CreateDirectory(huey);
   var fixtures=Path.Combine(AppContext.BaseDirectory,"Fixtures");
   var shared=Path.Combine(fixtures,"DcsCommon");
   foreach(var (name,path) in new[]{
    ("hardware.json","assets/shared/hardware/manifest.json"),
    ("functions.json","assets/shared/ui-layer/functions.json"),
    ("hardware-overlays.json","assets/shared/ui-layer/hardware-overlays.json"),
    ("device-map.json","assets/shared/hardware/scaffold-device-map.json")})
    Copy(Path.Combine(shared,name),common,path);
   var lua=Path.Combine(shared,"Lua");
   foreach(var source in Directory.GetFiles(lua,"*.lua",SearchOption.AllDirectories))
    Copy(source,common,Path.GetRelativePath(lua,source));
   var consumer=Path.Combine(fixtures,"Huey");
   foreach(var source in Directory.GetFiles(consumer,"*.*",SearchOption.AllDirectories))
    Copy(source,huey,Path.GetRelativePath(consumer,source));
   Commit(common);Commit(huey);
   File.WriteAllText(Path.Combine(huey,"config","kneeboard.json"),"{broken");
   var result=await new HueyWorkspaceLoader().LoadAsync(common,huey);
   Assert.True(result.Succeeded,string.Join("; ",result.Diagnostics.Select(x=>x.Message)));
   Assert.Equal(5,result.Configuration!.Profiles.Count);
   Assert.Equal(12,result.Provenance.Count);
   Assert.Equal(13,result.UiFunctionKeys.Count);
   Assert.StartsWith("{\n",result.Sources["config/kneeboard.json"],StringComparison.Ordinal);
  }
  finally {DeleteFixture(temp);}
 }
 /// <summary>Selection must point at a repository root rather than a nested directory.</summary>
 [Fact] public async Task Rejects_non_repository_folder() {
  var path=Path.Combine(Path.GetTempPath(),"DcsIpiaWorkspace-"+Guid.NewGuid().ToString("N"));
  try {
   Directory.CreateDirectory(path);
   await Assert.ThrowsAsync<InvalidDataException>(()=>new HueyWorkspaceLoader().LoadAsync(path,path));
  }
  finally {DeleteFixture(path);}
 }
 #endregion END Public
 #region BEGIN private helpers
 private static void DeleteFixture(string path) {
  if(!Directory.Exists(path))return;
  foreach(var file in Directory.EnumerateFiles(path,"*",SearchOption.AllDirectories))
   File.SetAttributes(file,FileAttributes.Normal);
  Directory.Delete(path,true);
 }
 private static void Copy(string source,string root,string path) {
  var target=Path.Combine(root,path);
  Directory.CreateDirectory(Path.GetDirectoryName(target)!);
  File.Copy(source,target);
 }
 private static void Commit(string root) {
  Git(root,"init","-q");
  Git(root,"add","--all");
  Git(root,"-c","user.name=Fixture","-c","user.email=fixture@example.invalid","commit","-qm","fixture");
 }
 private static void Git(string root,params string[] args) {
  using var process=new Process();
  process.StartInfo=new("git"){WorkingDirectory=root,RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false};
  foreach(var argument in args)process.StartInfo.ArgumentList.Add(argument);
  process.Start();
  var error=process.StandardError.ReadToEnd();
  process.WaitForExit();
  Assert.True(process.ExitCode==0,error);
 }
 #endregion END private helpers
}
