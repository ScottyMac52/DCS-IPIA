namespace Dcs.Ipia.Domain;
public sealed record Module {
 public Module(Guid id,string dcsId,string displayName) {
  if(id==Guid.Empty) throw new ArgumentException("A module ID is required.",nameof(id));
  if(string.IsNullOrWhiteSpace(dcsId)) throw new ArgumentException("A DCS module ID is required.",nameof(dcsId));
  if(string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("A display name is required.",nameof(displayName));
  Id=id; DcsId=dcsId.Trim(); DisplayName=displayName.Trim();
 }
 public Guid Id {get;} public string DcsId {get;} public string DisplayName {get;}
}
