using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
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
   var image=Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/ZQAAAABJRU5ErkJggg==");
   var imagePath=Path.Combine(huey,"kneeboard","UH-1H","00-UH1H-CONTROL-OVERVIEW.png");
   Directory.CreateDirectory(Path.GetDirectoryName(imagePath)!);
   File.WriteAllBytes(imagePath,image);
   using(var overlays=JsonDocument.Parse(File.ReadAllText(Path.Combine(common,"assets/shared/ui-layer/hardware-overlays.json")))) {
    foreach(var device in new[]{"tm-mfd","grip-f18c"}) {
     var controls=overlays.RootElement.GetProperty("devices").GetProperty(device).GetProperty("bindings")
      .EnumerateObject().Select(x=>x.Value.GetString()!).Distinct().ToArray();
     var rects=string.Join("",controls.Select((control,i)=>$"<!-- box:{control} --><rect x='10' y='{10+i*48}' width='55' height='45'/>"));
     var path=Path.Combine(common,"assets","shared","hardware","svg",device+".svg");
     Directory.CreateDirectory(Path.GetDirectoryName(path)!);
     File.WriteAllText(path,$"<svg viewBox='0 0 960 720'><image href='data:image/png;base64,{Convert.ToBase64String(image)}'/>{rects}</svg>");
    }
   }
   Commit(common);Commit(huey);
   File.WriteAllText(Path.Combine(huey,"config","kneeboard.json"),"{broken");
   var result=await new HueyWorkspaceLoader().LoadAsync(common,huey);
   Assert.True(result.Import.Succeeded,string.Join("; ",result.Import.Diagnostics.Select(x=>x.Message)));
   Assert.Equal(5,result.Import.Configuration!.Profiles.Count);
   Assert.Equal(12,result.Import.Provenance.Count);
   Assert.Equal(13,result.Import.UiFunctionKeys.Count);
   Assert.StartsWith("{\n",result.Import.Sources["config/kneeboard.json"],StringComparison.Ordinal);
   var preview=Assert.Single(result.Images);
   Assert.Equal("00-UH1H-CONTROL-OVERVIEW",preview.PageKey);
   Assert.Equal(image,preview.Bytes);
   Assert.Equal(2,result.Diagrams.Count);
   Assert.Equal(13,result.Diagrams.Single(x=>x.Instance=="MFD3").Callouts.Count);
   Assert.Equal(3,result.Diagrams.Single(x=>x.Instance is null).Callouts.Count);
   Assert.DoesNotContain(result.Diagrams,x=>x.Instance=="MFD1"||x.Instance=="MFD2");
   Assert.All(result.Diagrams,x=>Assert.Equal(image,x.Background));
   Assert.Contains(result.Diagrams.Single(x=>x.Instance=="MFD3").Callouts,
    x=>x.Function=="VR Zoom"&&x.Modifier=="MOZA_MODIFIER_BTN3");
   Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(image)),preview.Sha256);
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
