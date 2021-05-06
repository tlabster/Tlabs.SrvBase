using System;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

using Tlabs.Config;

namespace Tlabs.Server.Auth {
  ///<summary>Authorization filter to check for a configured API key.</summary>
  // https://randomkeygen.com/
  //256 bit: CIHgErMCjsAhfGvS8J5VHX3ZwInMGYgX
  public class ApiKeyAuthorizationFilter : IAsyncAuthorizationFilter {
    static readonly ILogger log= Tlabs.App.Logger<ApiKeyAuthorizationFilter>();
    const string HEADER_AUTH_KEY= "Authorization";
    Options authOptions;
    Regex pathPattern;
    ///<summary>Ctor from <paramref name="options"/>.</summary>
    public ApiKeyAuthorizationFilter(IOptions<Options> options) {
      this.authOptions= options.Value;
      this.pathPattern= new Regex(authOptions.pathPolicy, RegexOptions.Compiled);
    }
    ///<inheritdoc/>
    public Task OnAuthorizationAsync(AuthorizationFilterContext ctx) {
      // Skip filter if header does not contain an api key or action is marked as anonymous
      if (ctx.Filters.Any(item => item is IAllowAnonymousFilter)) { return Task.CompletedTask; }
      if (!ctx.HttpContext.Request.Headers.ContainsKey(HEADER_AUTH_KEY)) { return Task.CompletedTask; }

      var route= ctx.ActionDescriptor.AttributeRouteInfo.Template;
      string key= null;
      var authorize= ctx.HttpContext.Request.Headers[HEADER_AUTH_KEY];

      if (1 == authorize.Count) {
        var authParts= authorize[0].Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (2 == authParts.Length && 0 == string.Compare(authParts[0].Trim(), "ApiKey", StringComparison.OrdinalIgnoreCase))
          key= authParts[1];
      }
      if (   pathPattern.IsMatch(route)
          && null == key || key != authOptions.masterKey) {
        log.LogInformation("Unauthorized access: {path}", ctx.HttpContext.Request.Path);

        var err= new JsonResult(new {
          success= false,
          error= "Unauthorized Request"
        });
        err.StatusCode= StatusCodes.Status401Unauthorized;
        ctx.Result= err;  //setting a result does short-circuit the remainder of the filter pipeline...
      }
      return Task.CompletedTask;
    }

    ///<summary>Filter options.</summary>
    public class Options {
      ///<summary>Path policy regex pattern.</summary>
      public string pathPolicy { get; set; }
      ///<summary>Master API key.</summary>
      ///<remarks>Clear text (should not be used)</remarks>
      public string masterKey { get; set; }
    }
    /// <summary>Configurator</summary>
    public class Configurator : IConfigurator<IServiceCollection> {
      /// <inheritdoc/>
      public void AddTo(IServiceCollection svcColl, IConfiguration cfg) {
        svcColl.Configure<Options>(cfg.GetSection("config"));
        svcColl.AddSingleton<ApiKeyAuthorizationFilter>();
        ApiKeyAuthorizationFilter.log.LogInformation("Service {s} added.", nameof(ApiKeyAuthorizationFilter));
      }
    }

  }
}