using System;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc.Filters;

using Tlabs.Config;
using System.Security.Claims;
using System.Collections.Generic;
using System.Security.Principal;

namespace Tlabs.Server.Auth {
  ///<summary>Authorization filter to check for API keys stored in the database</summary>
  public class ApiKeyDBAuthFilter : BaseAuthFilter {
    readonly IApiKeyRegistry apiKeyRegistry;
    Regex pathPattern;

    ///<summary>Ctor</summary>
    public ApiKeyDBAuthFilter(IApiKeyRegistry apiKeyRegistry, IOptions<Options> options) {
      this.apiKeyRegistry= apiKeyRegistry;
      this.pathPattern= new Regex(options.Value.pathPolicy, RegexOptions.Compiled);
    }

    ///<inheritdoc/>
    public override void OnAuthorization(AuthorizationFilterContext context) {
      try {
        // Skip filter if header is marked as anonymous
        if (context.Filters.Any(item => item is IAllowAnonymousFilter)) return;

        // Skip filter if header does not contain an api header
        if (!context.HttpContext.Request.Headers.ContainsKey(HEADER_AUTH_KEY)) {
          // If no other filter is set also return unauthorized
          if (!context.Filters.Any(item => item is AuthCookieAuthorizationFilter)) {
            unauthorized(context);
          }
          return;
        }

        var key= extractKey(context);
        if (null == key) {
          unauthorized(context);
          return;
        }

        var token= apiKeyRegistry.VerifiedKey(key);
        if (null == token || null == token.Roles || !token.Roles.Any()) {
          unauthorized(context);
          return;
        }
        // In case API is used set new principal in context to set current user to API key
        var identity= new ClaimsIdentity("Identity.ApiKey");
        identity.AddClaim(new Claim(ClaimTypes.Name, token.TokenName));
        context.HttpContext.User= new System.Security.Claims.ClaimsPrincipal(
          new List<ClaimsIdentity> { identity }
        );

        if (checkRoles(token.Roles.ToArray(), context)) {
          return;
        }

        forbidden(context);
      }
      catch (Exception e) {
        errorResult(context, e);
      }
    }

    private string extractKey(AuthorizationFilterContext context) {
      string key= null;
      var route= context.ActionDescriptor.AttributeRouteInfo.Template;
      var authorize= context.HttpContext.Request.Headers[HEADER_AUTH_KEY];
      if (1 == authorize.Count) {
        var authParts= authorize[0].Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (2 == authParts.Length && 0 == string.Compare(authParts[0].Trim(), "ApiKey", StringComparison.InvariantCultureIgnoreCase))
          key= authParts[1];
      }

      if (!pathPattern.IsMatch(route)) return null;
      return key;
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