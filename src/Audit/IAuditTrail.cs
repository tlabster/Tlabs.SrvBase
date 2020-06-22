using Microsoft.AspNetCore.Http;
using Tlabs.Data.Model;

namespace Tlabs.Server.Audit {

  ///<summary>Registers audit trail of all activity which happens through .</summary>
  public interface IAuditTrail {
    ///<summary>Lists the audit trail history </summary>
    IResultList<Model.AuditRecord> List(QueryFilter filter);

    ///<summary>Lists the audit trail history </summary>
    Model.AuditRecord StoreTrail(HttpContext context, bool storeBody= false, System.Exception exception = null);
  }
}
