using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.ModelBinding;

using Tlabs.Config;

namespace Tlabs.Server.Url {

  ///<summary>URL decoder for decoded slash character.</summary>
  ///<remarks>This would be obsolete when using catch-all route parameters...</remarks>
  public class RouteParamDecodeFilter : IActionFilter {
    const string SLASH_ENC= "%2F";
    const string SLASH_DEC= "/";

    ///<inheritdoc/>>
    public void OnActionExecuted(ActionExecutedContext context) { }

    ///<inheritdoc/>>
    public void OnActionExecuting(ActionExecutingContext ctx) {
      foreach(var param in ctx.ActionDescriptor.Parameters.Where(p => BindingSource.Path == p.BindingInfo?.BindingSource
                                                                        && typeof(string) == p.ParameterType
                                                                        && ctx.ActionArguments.ContainsKey(p.Name))) {
        var val= ctx.ActionArguments[param.Name] as string;
        if (!string.IsNullOrEmpty(val)) {
          ctx.ActionArguments[param.Name]= val.Replace(SLASH_ENC, SLASH_DEC, System.StringComparison.OrdinalIgnoreCase);
        }
      }
    }

    ///<inheritdoc/>
    public class Configurator : IConfigurator<IServiceCollection> {
      /// <inheritdoc/>
      public void AddTo(IServiceCollection svcColl, IConfiguration cfg) {
        svcColl.AddSingleton(new RouteParamDecodeFilter());
      }
    }
  }
}