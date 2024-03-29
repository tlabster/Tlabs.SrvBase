﻿using System;
using System.Security.Claims;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Filters;

using Tlabs.Config;
using Tlabs.Identity;

namespace Tlabs.Server.Auth {
  ///<summary>Authorization filter to check for API keys from <see cref="IApiKeyRegistry"/>.</summary>
  public class ApiKeyRegistryAuthFilter : BaseAuthFilter {
    readonly IApiKeyRegistry apiKeyRegistry;
    readonly Regex pathPattern;

    ///<summary>Ctor</summary>
    public ApiKeyRegistryAuthFilter(IRolesAdministration rolesAdm, IApiKeyRegistry apiKeyRegistry, IOptions<Options> options) : base(rolesAdm) {
      this.apiKeyRegistry= apiKeyRegistry;
      this.pathPattern= new Regex(options.Value.pathPolicy ?? "", RegexOptions.Compiled);
    }

    ///<inheritdoc/>
    public override void OnAuthorization(AuthorizationFilterContext context) {
      try {
        // Skip filter if header does not contain an api header
        if (!context.HttpContext.Request.Headers.ContainsKey(HEADER_AUTH_KEY)) {
          // If no other filter is set also return unauthorized
          if (!context.Filters.Any(item => item is AuthCookieAuthorizationFilter)) {
            setUnauthorized(context);
          }
          return;
        }

        var key= extractKey(context);
        if (null == key) {
          setUnauthorized(context);
          return;
        }

        var token= apiKeyRegistry.VerifiedKey(key);
        if (null == token || null == token.Roles || 0 == token.Roles.Count) {
          setUnauthorized(context);
          return;
        }
        // In case API is used set new principal in context to set current user to API key
        var identity= new ClaimsIdentity("Identity.ApiKey");
        identity.AddClaim(new Claim(ClaimTypes.Name, token.TokenName ?? "<null>"));
        if(token.Roles != null)
          foreach(var role in token.Roles)
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
        context.HttpContext.User= new ClaimsPrincipal(
          new List<ClaimsIdentity> { identity }
        );

        if (checkRoles(token.Roles, context)) return;

        setForbidden(context);
      }
      catch (Exception e) {
        setError(context, e);
      }
    }

    private string? extractKey(AuthorizationFilterContext context) {
      var route= context.ActionDescriptor.AttributeRouteInfo?.Template;
      var authorize= context.HttpContext.Request.Headers[HEADER_AUTH_KEY];
      string? key= BaseAuthFilter.ParseAuthorizationKey(authorize);

      if (!pathPattern.IsMatch(route ?? "")) return null;
      return key;
    }


    ///<summary>Filter options.</summary>
    public class Options {
      ///<summary>Path policy regex pattern.</summary>
      public string? pathPolicy { get; set; }
    }

    /// <summary>Configurator</summary>
    public class Configurator : IConfigurator<IServiceCollection>, IConfigurator<IWebHostBuilder> {
      /// <inheritdoc/>
      public void AddTo(IServiceCollection svcColl, IConfiguration cfg) {
        svcColl.Configure<Options>(cfg.GetSection("config"));
        svcColl.Configure<Identity.Intern.SingletonApiKeyDataStoreRegistry.Options>(cfg.GetSection("config"));
        svcColl.AddSingleton<ApiKeyRegistryAuthFilter>();
        svcColl.AddSingleton<IApiKeyRegistry, Identity.Intern.SingletonApiKeyDataStoreRegistry>();
        log.LogInformation("Service {s} added.", nameof(ApiKeyAuthorizationFilter));
      }
      /// <inheritdoc/>
      public void AddTo(IWebHostBuilder hostBuilder, IConfiguration cfg)
        => hostBuilder.ConfigureServices(svcColl => AddTo(svcColl, cfg));
    }
  }
}