using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Tlabs.Data;
using Tlabs.Data.Entity;
using Tlabs.Data.Model;

namespace Tlabs.Server.Audit {
  ///<summary>Implementation of an audit logger</summary>
  public class AuditTrail : IAuditTrail {
    readonly IRepo<AuditRecord> repo;

    ///<summary>Ctor from <paramref name="repo"/></summary>
    public AuditTrail(IRepo<AuditRecord> repo) {
      this.repo= repo;
    }

    ///<inheritdoc/>
    public IResultList<Model.AuditRecord> List(QueryFilter filter) {
      var query= repo.AllUntracked;

      if (null != filter.Properties) foreach (var kv in filter.Properties) {
          if (filterMap.TryGetValue(kv.Key, out var fx))
            query= fx(query, kv.Value);
        }

      if (null != filter.SortAscBy && 0 != filter.SortAscBy.Count) foreach (var kv in filter.SortAscBy) {
          if (sorterMap.TryGetValue(kv.Key, out var sx))
            query= sx(query, kv.Value);
        }
      else query= query.OrderBy(d => d.Modified);

      var recs= query.Select(a => new Model.AuditRecord {
        AccessTime= a.Modified,
        ActionName= a.ActionName,
        BodyData= a.BodyData,
        Editor= a.Editor,
        IPAddress= a.IPAddress,
        Method= a.Method,
        StatusCode= a.StatusCode,
        Error= a.Error,
        Success= a.Success,
        URL= a.URL
      });

      var limit= recs;
      if (filter.Start.HasValue)
        limit= limit.Skip(filter.Start.Value);
      if (filter.Limit.HasValue)
        limit= limit.Take(filter.Limit.Value);

      return new QueryResult<Model.AuditRecord> {
        Total= recs.Count(),
        Data= limit.ToList()
      };
    }

    ///<inheritdoc/>
    public Model.AuditRecord StoreTrail(FilterContext context, bool storeBody= false) {
      var httpContext= context.HttpContext;
      var request = httpContext.Request;
      var connection= httpContext.Connection;

      if(HttpMethods.IsGet(httpContext.Request.Method)) return null;

      var audit= new AuditRecord {
        ActionName= httpContext.GetRouteData().Values["Controller"].ToString() + "/" + httpContext.GetRouteData().Values["Action"].ToString(),
        IPAddress= connection.RemoteIpAddress.ToString(),
        URL= Microsoft.AspNetCore.Http.Extensions.UriHelper.GetDisplayUrl(request),
        Method= httpContext.Request.Method,
        StatusCode= httpContext.Response.StatusCode.ToString(App.DfltFormat)
      };

      if(context is ResultExecutingContext) {
        var c= context as ResultExecutingContext;
        if (c.Result is Model.BaseCover res) {
          audit.Error= res.error;

          audit.Error= null != res.errDetails.msgData && res.errDetails.msgData.ContainsKey("joinedErrors") ?
            res.errDetails.msgData["joinedErrors"] as string : res.error;
        }
      } else if(context is ExceptionContext) {
        var c= context as ExceptionContext;
        audit.Error= c.Exception.Message;
      }

      if((!audit.Success || storeBody) && null != request.Body && request.Body.CanSeek) {
        request.Body.Position= 0;
        audit.BodyData= readBody(request.Body);
      }

      repo.Insert(audit);
      repo.Store.CommitChanges();
      return null;
    }

    static string readBody(Stream st) {
      using var reader= new StreamReader(st);
      return reader.ReadToEnd();
    }

    static readonly IDictionary<string, QueryFilter.FilterExpression<AuditRecord>> filterMap= new Dictionary<string, QueryFilter.FilterExpression<AuditRecord>>(StringComparer.OrdinalIgnoreCase) {
      [nameof(Model.AuditRecord.URL)]= (q, cv) => q.Where(m => m.URL.Contains(cv.ToString())),
      [nameof(Model.AuditRecord.Editor)]= (q, cv) => q.Where(m => m.Editor.Contains(cv.ToString())),
      [nameof(Model.AuditRecord.Method)]= (q, cv) => q.Where(m => m.Method.StartsWith(cv.ToString())),
      [nameof(Model.AuditRecord.IPAddress)]= (q, cv) => q.Where(m => m.IPAddress.Contains(cv.ToString())),
      [nameof(Model.AuditRecord.ActionName)]= (q, cv) => q.Where(m => m.ActionName.Contains(cv.ToString())),
      [nameof(Model.AuditRecord.StatusCode)]= (q, cv) => q.Where(m => m.StatusCode.StartsWith(cv.ToString())),
      [nameof(Model.AuditRecord.Success)]= (q, cv) => q.Where(m => m.Success == cv.ToBoolean(CultureInfo.InvariantCulture))
    };

    static readonly IDictionary<string, QueryFilter.SorterExpression<AuditRecord>> sorterMap= new Dictionary<string, QueryFilter.SorterExpression<AuditRecord>>(StringComparer.OrdinalIgnoreCase) {
      [nameof(Model.AuditRecord.URL)]= (q, isAsc) => isAsc ? q.OrderBy(m => m.URL) : q.OrderByDescending(m => m.URL),
      [nameof(Model.AuditRecord.Editor)]= (q, isAsc) => isAsc ? q.OrderBy(m => m.Editor) : q.OrderByDescending(m => m.Editor),
      [nameof(Model.AuditRecord.Method)]= (q, isAsc) => isAsc ? q.OrderBy(m => m.Method) : q.OrderByDescending(m => m.Method),
      [nameof(Model.AuditRecord.IPAddress)]= (q, isAsc) => isAsc ? q.OrderBy(m => m.IPAddress) : q.OrderByDescending(m => m.IPAddress),
      [nameof(Model.AuditRecord.AccessTime)]= (q, isAsc) => isAsc ? q.OrderBy(m => m.Modified) : q.OrderByDescending(m => m.Modified),
      [nameof(Model.AuditRecord.StatusCode)]= (q, isAsc) => isAsc ? q.OrderBy(m => m.StatusCode) : q.OrderByDescending(m => m.StatusCode),
      [nameof(Model.AuditRecord.Success)]= (q, isAsc) => isAsc ? q.OrderBy(m => m.Success) : q.OrderByDescending(m => m.Success)
    };

  }
}