﻿using System;
using System.Collections.Generic;
using Tlabs.Data.Entity;
namespace Tlabs.Server.Model {
  ///<summary>Model class for the API Key</summary>
  public class KeyToken {
    ///<summary>(Unique) name of the token</summary>
    public string TokenName { get; set; }

    ///<summary>Description</summary>
    public string Description { get; set; }

    ///<summary>Valid from (inclusive)</summary>
    public DateTime? ValidFrom { get; set; }

    ///<summary>Valid until (exclusive)</summary>
    public DateTime? ValidUntil { get; set; }

    ///<summary>Roles</summary>
    public List<Role> Roles { get; set; }

    ///<summary>Validity State</summary>
    public string ValidityState { get; set; }

    ///<summary>Returns an API Key Model from the entity</summary>
    public static KeyToken FromEntity(Tlabs.Data.Entity.ApiKey ent) {
      if (ent == null)
        return null;
      var key= new KeyToken {
        TokenName= ent.TokenName,
        Description= ent.Description,
        ValidFrom= ent.ValidFrom,
        ValidUntil= ent.ValidUntil,
        ValidityState= ent.ValidityState
      };
      return key;
    }
  }
}