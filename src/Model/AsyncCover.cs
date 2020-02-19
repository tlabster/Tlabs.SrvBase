using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;

namespace Tlabs.Server.Model {

  ///<summary>Cover for a single model object being provided from async delegate.</summary>
  public class AsyncModelCover<T> : ModelCover<T>, IActionResult {
    Task<T> resTask;
    Func<Exception, string> provideErrMessage;

    ///<summary>Ctor from async <paramref name="provideModel"/> and (optional) <paramref name="provideErrMessage"/> delegates.</summary>
    ///<remarks>Use with a controller like:
    ///<code>
    ///[HttpPut]
    ///public AsyncModelCover&lt;MyModel&gt; Update([FromBody]MyModel model) {
    ///  return new AsyncModelCoverr&lt;MyModel&gt;(async (cover) => {
    ///    /* do anything async to obtain the model: */
    ///    return await asyncObtainModel(...);
    ///  });
    ///}
    ///</code>
    ///</remarks>
    public AsyncModelCover(Func<ModelCover<T>, Task<T>> provideModel, Func<Exception, string> provideErrMessage = null) {
      this.resTask= provideModel(this);   //start task
      this.provideErrMessage= provideErrMessage;
    }

    ///<inheritdoc/>
    public async Task ExecuteResultAsync(ActionContext ctx) {
      try { this.data= await resTask; }
      catch (Exception e) {
        handleException(e, provideErrMessage);
      }
      await new ObjectResult(this) { DeclaredType= typeof(ModelCover<T>) }.ExecuteResultAsync(ctx);
    }
  }

  ///<summary>Cover for the result of a data query returning an <see cref="IEnumerable{T}"/>.</summary>
  public class AsyncQueryCover<T> : QueryCover<T>, IActionResult {
    Task<IEnumerable<T>> resTask;
    Func<Exception, string> provideErrMessage;
    ///<summary>Ctor from async <paramref name="queryResult"/> and (optional) <paramref name="provideErrMessage"/> delegates.</summary>
    public AsyncQueryCover(Func<QueryCover<T>, Task<IEnumerable<T>>> queryResult, Func<Exception, string> provideErrMessage = null) {
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

}