using System.Security.Cryptography;
using System.Text;

namespace Dcs.Ipia.Domain;

/// <summary>Describes a DCS command binding.</summary>
public sealed record DcsBinding {
 /// <summary>Initializes a new DCS binding.</summary>
 /// <param name="profileId">The profile identifier.</param>
 /// <param name="section">The section.</param>
 /// <param name="commandId">The command identifier.</param>
 /// <param name="commandName">The command name.</param>
 /// <param name="disposition">The disposition.</param>
 /// <param name="inputKey">The input key.</param>
 /// <param name="reformers">The reformers.</param>
 /// <param name="filter">The filter.</param>
 public DcsBinding(Guid profileId,BindingSection section,string commandId,string commandName,BindingDisposition disposition,string inputKey,IEnumerable<string>? reformers=null,AxisFilter? filter=null) {
  if(profileId==Guid.Empty) throw new ArgumentException("A profile ID is required.",nameof(profileId));
  if(section==BindingSection.Key && filter is not null) throw new ArgumentException("Only axis bindings may have a filter.",nameof(filter));
  ProfileId=profileId; Section=section; CommandId=Required(commandId,nameof(commandId)); CommandName=Required(commandName,nameof(commandName)); Disposition=disposition; InputKey=Required(inputKey,nameof(inputKey)); Filter=filter;
  Reformers=(reformers??[]).Select(x=>Required(x,nameof(reformers))).Order(StringComparer.Ordinal).ToArray();
  if(Reformers.Distinct(StringComparer.Ordinal).Count()!=Reformers.Count) throw new ArgumentException("A modifier chord cannot contain duplicates.",nameof(reformers));
 }
 #region BEGIN Public
 /// <summary>Gets the profile identifier.</summary>
 public Guid ProfileId {get;}
 /// <summary>Gets the section.</summary>
 public BindingSection Section {get;}
 /// <summary>Gets the command identifier.</summary>
 public string CommandId {get;}
 /// <summary>Gets the command name.</summary>
 public string CommandName {get;}
 /// <summary>Gets the disposition.</summary>
 public BindingDisposition Disposition {get;}
 /// <summary>Gets the input key.</summary>
 public string InputKey {get;}
 /// <summary>Gets the reformers.</summary>
 public IReadOnlyList<string> Reformers {get;}
 /// <summary>Gets the filter.</summary>
 public AxisFilter? Filter {get;}
 #endregion END Public
 #region BEGIN private helpers
 private static string Required(string value,string name)=>string.IsNullOrWhiteSpace(value)?throw new ArgumentException("A value is required.",name):value.Trim();
 #endregion END private helpers
}
