﻿using Microsoft.AspNetCore.Mvc.Filters;
using Tlabs.Data.Model;

namespace Tlabs.Server.Audit {

  ///<summary>Registers audit trail of all activity which happens through .</summary>
  public interface IAuditTrail {
    ///<summary>Lists the audit trail history </summary>
    IResultList<Model.AuditRecord> List(QueryFilter filter);

    ///<summary>Store trail</summary>
    Model.AuditRecord? StoreTrail(FilterContext context, bool storeBody= false);
  }
}
