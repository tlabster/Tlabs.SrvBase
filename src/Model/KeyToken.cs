using System;
using System.Collections.Generic;
using System.Linq;

namespace Tlabs.Server.Model {
  ///<summary>Model class for the API Key</summary>
  public class KeyToken {
    ///<summary>(Unique) name of the token</summary>
    public string? TokenName { get; set; }

    ///<summary>Description</summary>
    public string? Description { get; set; }

    ///<summary>Valid from (inclusive)</summary>
    public DateTime? ValidFrom { get; set; }

    ///<summary>Valid until (exclusive)</summary>
    public DateTime? ValidUntil { get; set; }

    ///<summary>Roles</summary>
    public List<string>? Roles { get; set; }

    ///<summary>Validity State</summary>
    public string? ValidityState { get; set; }

    ///<summary>Returns an API Key Model from the entity</summary>
    public static KeyToken FromEntity(Tlabs.Data.Entity.ApiKey ent) {
      var key= new KeyToken {
        TokenName= ent.TokenName,
        Description= ent.Description,
        ValidFrom= ent.ValidFrom,
        ValidUntil= ent.ValidUntil,
        ValidityState= ent.ValidityState,
        Roles= ent.Roles?.Where(r => null != r.Role?.Name).Select(x => x.Role!.Name).ToList()!
      };
      return key;
    }
  }
}