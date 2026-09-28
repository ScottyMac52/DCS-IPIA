using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Dcs.Ipia.Packaging;
using Xunit;

namespace Dcs.Ipia.Tests;

/// <summary>Verifies consumer-compatible OvGME archive structure and reproducibility.</summary>
public sealed class OvGmePackageBuilderTests {
 private static readonly byte[] Profile=Encoding.UTF8.GetBytes("return { [\"added\"] = {} }");
 private static readonly byte[] Png=[137,80,78,71,13,10,26,10];
 #region BEGIN Public
 /// <summary>Archive structure matches the consumer package contract, independent of input order.</summary>
 [Fact] public void Builds_complete_deterministic_archive() {
  var a=Input(new Dictionary<string,byte[]> {
   ["KNEEBOARD/UH-1H/01-MFD.png"]=Png,
   ["Config/Input/UiLayer/joystick/MFD.diff.lua"]=Profile,
   ["Config/Input/UH-1H/joystick/MFD.diff.lua"]=Profile,
   ["Config/Input/UiLayer/modifiers.lua"]=Encoding.UTF8.GetBytes("return {}"),
   ["Config/Input/UH-1H/modifiers.lua"]=Encoding.UTF8.GetBytes("return {}")
  });
  var b=Input(a.Payload.Reverse().ToDictionary(x=>x.Key,x=>x.Value));
  var first=OvGmePackageBuilder.Build(a);
  var second=OvGmePackageBuilder.Build(b);
  Assert.Equal(first.Bytes,second.Bytes);
  Assert.Equal("DCS-UH-1H-Components-v1-OVGME.zip",first.FileName);
  Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(first.Bytes)),first.Sha256);
  Assert.Equal($"{first.Sha256}  {first.FileName}\n",first.ChecksumLine);
  using var stream=new MemoryStream(first.Bytes);
  using var zip=new ZipArchive(stream,ZipArchiveMode.Read);
  var root="DCS-UH-1H-Components-v1-OVGME/";
  Assert.Equal(["README.TXT","VERSION.TXT",root+"Config/Input/UH-1H/joystick/MFD.diff.lua",
   root+"Config/Input/UH-1H/modifiers.lua",root+"Config/Input/UiLayer/joystick/MFD.diff.lua",
   root+"Config/Input/UiLayer/modifiers.lua",root+"KNEEBOARD/UH-1H/01-MFD.png"],
   zip.Entries.Select(x=>x.FullName));
  Assert.All(zip.Entries,x=>Assert.Equal(1980,x.LastWriteTime.Year));
  using var reader=new StreamReader(zip.GetEntry("README.TXT")!.Open());
  Assert.Contains("Install relative to your DCS Saved Games root.",reader.ReadToEnd(),StringComparison.Ordinal);
  Assert.Equal(Png,Read(zip.GetEntry(root+"KNEEBOARD/UH-1H/01-MFD.png")!));
  Assert.Equal("v1",Encoding.UTF8.GetString(Read(zip.GetEntry("VERSION.TXT")!)));
 }
 /// <summary>Partial or surprising payloads never become plausible release archives.</summary>
 [Theory]
 [InlineData("Config/Input/UH-1H/joystick/../bad.diff.lua")]
 [InlineData("Config\\Input\\UH-1H\\joystick\\bad.diff.lua")]
 [InlineData("/Config/Input/UH-1H/joystick/bad.diff.lua")]
 [InlineData("Config/Input/UH-1H/joystick/bad.txt")]
 [InlineData("Config/Input/F-16C/joystick/bad.diff.lua")]
 [InlineData("README.TXT")]
 public void Rejects_unsafe_or_unexpected_paths(string path) {
  var payload=BasePayload();payload[path]=Profile;
  Assert.Throws<ArgumentException>(()=>OvGmePackageBuilder.Build(Input(payload)));
 }
 /// <summary>Case-insensitive collision cannot overwrite a Windows Saved Games file.</summary>
 [Fact] public void Rejects_case_collisions() {
  var payload=BasePayload();payload["KNEEBOARD/UH-1H/01-mfd.png"]=Png;
  Assert.Throws<ArgumentException>(()=>OvGmePackageBuilder.Build(Input(payload)));
 }
 /// <summary>At least one source profile, image, and UI modifier file are mandatory as applicable.</summary>
 [Fact] public void Rejects_incomplete_payloads() {
  var noProfile=new Dictionary<string,byte[]> {{"KNEEBOARD/UH-1H/01-MFD.png",Png}};
  Assert.Throws<ArgumentException>(()=>OvGmePackageBuilder.Build(Input(noProfile)));
  var noPng=new Dictionary<string,byte[]> {{"Config/Input/UH-1H/joystick/MFD.diff.lua",Profile}};
  Assert.Throws<ArgumentException>(()=>OvGmePackageBuilder.Build(Input(noPng)));
  var noUiModifiers=BasePayload();noUiModifiers["Config/Input/UiLayer/joystick/MFD.diff.lua"]=Profile;
  Assert.Throws<ArgumentException>(()=>OvGmePackageBuilder.Build(Input(noUiModifiers)));
  var nullFile=BasePayload();nullFile["KNEEBOARD/UH-1H/02-MFD.png"]=null!;
  Assert.Throws<ArgumentException>(()=>OvGmePackageBuilder.Build(Input(nullFile)));
 }
 /// <summary>Package names and versions cannot become paths in the archive.</summary>
 [Theory]
 [InlineData("../escape","UH-1H","v1")]
 [InlineData("Package","UH/1H","v1")]
 [InlineData("Package","UH-1H","v1/../../bad")]
 [InlineData("Package","UH-1H","")]
 public void Rejects_bad_identifiers(string packageId,string moduleId,string version) {
  Assert.Throws<ArgumentException>(()=>OvGmePackageBuilder.Build(new(packageId,moduleId,version,BasePayload())));
 }
 #endregion END Public
 #region BEGIN private helpers
 private static OvGmePackageInput Input(IReadOnlyDictionary<string,byte[]> payload)=>new("DCS-UH-1H-Components","UH-1H","v1",payload);
 private static Dictionary<string,byte[]> BasePayload()=>new() {
  ["Config/Input/UH-1H/joystick/MFD.diff.lua"]=Profile,
  ["KNEEBOARD/UH-1H/01-MFD.png"]=Png
 };
 private static byte[] Read(ZipArchiveEntry entry) {
  using var input=entry.Open();using var output=new MemoryStream();input.CopyTo(output);return output.ToArray();
 }
 #endregion END private helpers
}
