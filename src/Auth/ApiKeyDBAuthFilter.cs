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
using Tlabs.Data;
using Microsoft.AspNetCore.Identity;
using System.Collections.Generic;
using Tlabs.Server.Model;

namespace Tlabs.Server.Auth {
  ///<summary>Authorization filter to check for API keys stored in the database</summary>
  public class ApiKeyDBAuthFilter : IAsyncAuthorizationFilter {
    static readonly ILogger log= Tlabs.App.Logger<ApiKeyDBAuthFilter>();
    const string HEADER_AUTH_KEY= "Authorization";
    readonly IApiKeyRegistry apiKeyRegistry;
    Regex pathPattern;

    ///<summary>Ctor</summary>
    public ApiKeyDBAuthFilter(IApiKeyRegistry apiKeyRegistry, IOptions<Options> options) {
      this.apiKeyRegistry= apiKeyRegistry;
      this.pathPattern= new Regex(options.Value.pathPolicy, RegexOptions.Compiled);
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
        if (2 == authParts.Length && 0 == string.Compare(authParts[0].Trim(), "ApiKey", StringComparison.InvariantCultureIgnoreCase))
          key= authParts[1];
      }
      if (!pathPattern.IsMatch(route))
        return Task.CompletedTask;
      if (null == key)
        return unauthorized(ctx);

      var token= apiKeyRegistry.VerifiedKey(key);
      if (token == null)
        return unauthorized(ctx);

      return Task.CompletedTask;
    }

    private Task unauthorized(AuthorizationFilterContext ctx) {
      log.LogInformation("Unauthorized access: {path}", ctx.HttpContext.Request.Path);

      var err= new JsonResult(new {
        success= false,
        error= "Unauthorized Request"
      });
      err.StatusCode= StatusCodes.Status401Unauthorized;
      ctx.Result= err;  //setting a result does short-circuit the remainder of the filter pipeline...
      return Task.CompletedTask;
    }

    ///<summary>Filter options.</summary>
    public class Options {
      ///<summary>Path policy regex pattern.</summary>
      public string pathPolicy { get; set; }
    }

    /// <summary>Configurator</summary>
    public class Configurator : IConfigurator<IServiceCollection> {
      /// <inheritdoc/>
      public void AddTo(IServiceCollection svcColl, IConfiguration cfg) {
        svcColl.Configure<Options>(cfg.GetSection("config"));
        svcColl.Configure<DefaultApiKeyRegistry.Options>(cfg.GetSection("config"));
        svcColl.AddSingleton<ApiKeyDBAuthFilter>();
        svcColl.AddSingleton<IApiKeyRegistry, DefaultApiKeyRegistry>();
        ApiKeyDBAuthFilter.log.LogInformation("Service {s} added.", nameof(ApiKeyAuthorizationFilter));
      }
    }
  }
}