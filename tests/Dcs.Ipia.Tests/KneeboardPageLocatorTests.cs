using Dcs.Ipia.Application;
using Dcs.Ipia.Domain;
using Xunit;

namespace Dcs.Ipia.Tests;

/// <summary>Checks authored profile to rendered device page resolution.</summary>
public sealed class KneeboardPageLocatorTests {
 #region BEGIN Public
 /// <summary>Repeated devices select their own page from kneeboard.json instead of the first matching model.</summary>
 [Fact] public void Selected_profile_resolves_its_own_rendered_page() {
  var module=new DcsModule(Guid.NewGuid(),"TEST","Test");
  var one=new DeviceProfile(Guid.NewGuid(),"tm-mfd","MFD1","MFD1","MFD 1.diff.lua");
  var two=new DeviceProfile(Guid.NewGuid(),"tm-mfd","MFD2","MFD2","MFD 2.diff.lua");
  var config=new ModuleConfiguration(module,[one,two],[],[],"test");
  const string source="""{"profiles":{"first":"src/Config/Input/TEST/joystick/MFD 1.diff.lua","second":"src/Config/Input/TEST/joystick/MFD 2.diff.lua"},"pages":[{"profile":"first","file":"page-1"},{"profile":"second","file":"page-2"}]}""";
  var snapshot=new EditorWorkspaceSnapshot(config,[],[
   new KneeboardImageAsset("page-1","kneeboard/TEST/page-1.png",[1],"one"),
   new KneeboardImageAsset("page-2","kneeboard/TEST/page-2.png",[2],"two")
  ],[],[],[],[],new Dictionary<string,string>(),new Dictionary<string,string> {["config/kneeboard.json"]=source});
  Assert.Equal("page-2",KneeboardPageLocator.ForProfile(snapshot,two.Id)?.PageKey);
  Assert.Equal("page-1",KneeboardPageLocator.ForProfile(snapshot,one.Id)?.PageKey);
  Assert.Null(KneeboardPageLocator.ForProfile(snapshot,Guid.NewGuid()));
 }
 #endregion END Public
}
