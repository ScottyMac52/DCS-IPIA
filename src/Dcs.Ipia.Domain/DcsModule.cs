namespace Dcs.Ipia.Domain;

/// <summary>Represents DCS module.</summary>
public sealed record DcsModule {
 /// <summary>Initializes a new DCS module.</summary>
 /// <param name="id">The identifier.</param>
 /// <param name="dcsId">The DCS identifier.</param>
 /// <param name="displayName">The display name.</param>
 public DcsModule(Guid id,string dcsId,string displayName) {
  if(id==Guid.Empty) throw new ArgumentException("A module ID is required.",nameof(id));
  if(string.IsNullOrWhiteSpace(dcsId)) throw new ArgumentException("A DCS module ID is required.",nameof(dcsId));
  if(string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("A display name is required.",nameof(displayName));
  Id=id; DcsId=dcsId.Trim(); DisplayName=displayName.Trim();
 }
 #region BEGIN Public
 /// <summary>Gets the identifier.</summary>
 public Guid Id {get;}
 /// <summary>Gets the DCS identifier.</summary>
 public string DcsId {get;}
 /// <summary>Gets the display name.</summary>
 public string DisplayName {get;}
 #endregion END Public
}
