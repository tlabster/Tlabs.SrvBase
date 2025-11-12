using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;

using Tlabs.Data.Model;

namespace Tlabs.Server.Model {

  ///<summary>Cover for a single model object being provided from async delegate.</summary>
  public class AsyncModelCover<T> : ModelCover<T> {
    readonly Task<T> resTask;
    readonly Func<Exception, string>? provideErrMessage;

    ///<summary>Ctor from async <paramref name="provideModel"/> and (optional) <paramref name="provideErrMessage"/> delegates.</summary>
    ///<remarks>On a controller, it must be mapped to a standard ModelCover using <see cref="AsModelCoverAsync"/></remarks>
    public AsyncModelCover(Func<ModelCover<T>, Task<T>> provideModel, Func<Exception, string>? provideErrMessage = null) {
      this.resTask= provideModel(this);
      this.provideErrMessage= provideErrMessage;
    }

    /// <summary>
    /// Resolves AsyncModelCover to a standard ModelCover awaiting the task
    /// </summary>
    public async Task<ModelCover<T>> AsModelCoverAsync() {
      try {
        this.data = await resTask;
      }
      catch (Exception e) {
        handleException(e, provideErrMessage);
      }

      return this;
    }
  }

  ///<summary>Cover for the result of a data query returning an <see cref="IEnumerable{T}"/>.</summary>
  public class AsyncQueryCover<T> : QueryCover<T>, IActionResult {
    readonly Task<IEnumerable<T>> resTask;
    readonly Func<Exception, string>? provideErrMessage;
    ///<summary>Ctor from async <paramref name="queryResult"/> and (optional) <paramref name="provideErrMessage"/> delegates.</summary>
    public AsyncQueryCover(Func<QueryCover<T>, Task<IEnumerable<T>>> queryResult, Func<Exception, string>? provideErrMessage = null) {
      this.resTask= queryResult(this);
      this.provideErrMessage= provideErrMessage;
    }

    ///<inheritdoc/>
    public async Task ExecuteResultAsync(ActionContext ctx) {
      try { this.data= await resTask; }
      catch (Exception e) {
        handleException(e, provideErrMessage);
      }
      await new ObjectResult(this) { DeclaredType= typeof(QueryCover<T>) }.ExecuteResultAsync(ctx);
    }
  }

  ///<summary>Cover for the result of a data query returning an <see cref="IEnumerable{T}"/>.</summary>
  public class AsyncPagedQueryCover<T> : PagedQueryCover<T>, IActionResult {
    readonly Task<IResultList<T>> resTask;
    readonly Func<Exception, string>? provideErrMessage;

    ///<summary>Ctor from async <paramref name="queryResult"/> and (optional) <paramref name="provideErrMessage"/> delegates.</summary>
    public AsyncPagedQueryCover(Func<PagedQueryCover<T>, Task<IResultList<T>>> queryResult, Func<Exception, string>? provideErrMessage = null) {
      resTask = queryResult(this);
      this.provideErrMessage = provideErrMessage;
    }

    ///<inheritdoc/>
    public async Task ExecuteResultAsync(ActionContext ctx) {
      try {
        var res = await resTask;
        data = res.Data;
        total = res.Total;
      }
      catch (Exception e) {
        handleException(e, provideErrMessage);
      }
      await new ObjectResult(this) { DeclaredType = typeof(AsyncQueryCover<T>) }.ExecuteResultAsync(ctx);
    }
  }
}