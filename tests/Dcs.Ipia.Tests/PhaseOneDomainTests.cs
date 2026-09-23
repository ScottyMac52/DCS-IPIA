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

/// <summary>Represents phase one domain tests.</summary>
public sealed class PhaseOneDomainTests {
 private static readonly Guid ProfileId=Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
 #region BEGIN Public
 /// <summary>stable identity is deterministic and scoped.</summary>
 [Fact] public void Stable_identity_is_deterministic_and_scoped(){var first=StableIdentity.Create("UH-1H","tm-mfd\0MFD1");Assert.Equal(first,StableIdentity.Create(" UH-1H "," tm-mfd\0MFD1 "));Assert.NotEqual(first,StableIdentity.Create("UH-1H","tm-mfd\0MFD2"));Assert.Throws<ArgumentException>(()=>StableIdentity.Create("","x"));Assert.Throws<ArgumentException>(()=>StableIdentity.Create("x"," "));}
 /// <summary>records normalize values.</summary>
 [Fact] public void Records_normalize_values(){var profile=new DeviceProfile(ProfileId," tm-mfd "," MFD1 "," panel "," source.lua ");var modifier=new DcsModifier(" Shift "," Stick "," BTN3 ",ModifierMode.Hold);var binding=new DcsBinding(ProfileId,BindingSection.Axis," axis "," Roll ",BindingDisposition.Changed," JOY_X ",["Shift"],new(Invert:true));Assert.Equal("tm-mfd",profile.DeviceId);Assert.Equal("MFD1",profile.Instance);Assert.Equal("Shift",modifier.Name);Assert.Equal("axis",binding.CommandId);Assert.True(binding.Filter!.Invert is true);}
 /// <summary>invalid records are rejected.</summary>
 [Fact] public void Invalid_records_are_rejected(){Assert.Throws<ArgumentException>(()=>new DeviceProfile(Guid.Empty,"x",null,"r","s"));Assert.Throws<ArgumentException>(()=>new DeviceProfile(ProfileId,"","", "r","s"));Assert.Throws<ArgumentException>(()=>new DeviceProfile(ProfileId,"x",null,"","s"));Assert.Throws<ArgumentException>(()=>new DeviceProfile(ProfileId,"x",null,"r",""));Assert.Throws<ArgumentException>(()=>new DcsModifier("","d","k",ModifierMode.Hold));Assert.Throws<ArgumentException>(()=>new DcsModifier("m","","k",ModifierMode.Hold));Assert.Throws<ArgumentException>(()=>new DcsModifier("m","d","",ModifierMode.Hold));Assert.Throws<ArgumentException>(()=>new DcsBinding(Guid.Empty,BindingSection.Key,"c","n",BindingDisposition.Added,"k"));Assert.Throws<ArgumentException>(()=>new DcsBinding(ProfileId,BindingSection.Key,"c","n",BindingDisposition.Added,"k",filter:new(Invert:true)));Assert.Throws<ArgumentException>(()=>new DcsBinding(ProfileId,BindingSection.Key,"","n",BindingDisposition.Added,"k"));Assert.Throws<ArgumentException>(()=>new DcsBinding(ProfileId,BindingSection.Key,"c","",BindingDisposition.Added,"k"));Assert.Throws<ArgumentException>(()=>new DcsBinding(ProfileId,BindingSection.Key,"c","n",BindingDisposition.Added,""));Assert.Throws<ArgumentException>(()=>new DcsBinding(ProfileId,BindingSection.Key,"c","n",BindingDisposition.Added,"k",["A","A"]));}
 /// <summary>configuration enforces identity and reference integrity.</summary>
 [Fact] public void Configuration_enforces_identity_and_reference_integrity(){var module=new DcsModule(Guid.NewGuid(),"UH-1H","Huey");var profile=new DeviceProfile(ProfileId,"mfd","MFD1","panel","mfd.lua");var modifier=new DcsModifier("Shift","Stick","BTN3",ModifierMode.Hold);var binding=new DcsBinding(ProfileId,BindingSection.Key,"c","Command",BindingDisposition.Added,"BTN1",["Shift"]);var value=new ModuleConfiguration(module,[profile],[modifier],[binding],"repo@sha");Assert.Equal("repo@sha",value.Provenance);Assert.Throws<ArgumentNullException>(()=>new ModuleConfiguration(null!,[],[],[],"p"));Assert.Throws<ArgumentNullException>(()=>new ModuleConfiguration(module,null!,[],[],"p"));Assert.Throws<ArgumentNullException>(()=>new ModuleConfiguration(module,[],null!,[],"p"));Assert.Throws<ArgumentNullException>(()=>new ModuleConfiguration(module,[],[],null!,"p"));Assert.Throws<ArgumentException>(()=>new ModuleConfiguration(module,[profile,profile],[],[],"p"));Assert.Throws<ArgumentException>(()=>new ModuleConfiguration(module,[profile,new(Guid.NewGuid(),"MFD","mfd1","panel","two")],[],[],"p"));Assert.Throws<ArgumentException>(()=>new ModuleConfiguration(module,[profile],[modifier,new("shift","Other","BTN4",ModifierMode.Hold)],[],"p"));Assert.Throws<ArgumentException>(()=>new ModuleConfiguration(module,[profile],[modifier,new("Other","stick","btn3",ModifierMode.Hold)],[],"p"));Assert.Throws<ArgumentException>(()=>new ModuleConfiguration(module,[profile],[],[binding],"p"));Assert.Throws<ArgumentException>(()=>new ModuleConfiguration(module,[profile],[modifier],[new(Guid.NewGuid(),BindingSection.Key,"c","n",BindingDisposition.Added,"k")],"p"));Assert.Throws<ArgumentException>(()=>new ModuleConfiguration(module,[profile],[],[],""));}
 #endregion END Public
}
