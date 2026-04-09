using System;
using System.Linq;
using System.Net.Http;
using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Authorization;

using Tlabs.Config;

namespace Tlabs.Server.Auth.Keycloak {
  ///<summary>Authorization filter for Keycloak.</summary>
  public class KeycloakAuthorizationFilter : IAsyncAuthorizationFilter {
    static readonly ILogger log = Tlabs.App.Logger<KeycloakAuthorizationFilter>();
    readonly IKeycloakTokenService keycloakPermissionService;

    ///<summary>Ctor.</summary>
    public KeycloakAuthorizationFilter(
      IKeycloakTokenService keycloakPermissionService
    ) {
      this.keycloakPermissionService = keycloakPermissionService;
    }

    ///<inheritdoc/>
    public async Task OnAuthorizationAsync(AuthorizationFilterContext ctx) {
      // Skip filter if header does not contain an api key or action is marked as anonymous
      if (ctx.Filters.Any(item => item is IAllowAnonymousFilter)) { return; }
      var request = ctx.HttpContext.Request;

      // Extract Bearer token
      if (!request.Headers.TryGetValue("Authorization", out var authHeader) ||
          !authHeader.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) {
        Deny(ctx, "Missing or invalid Authorization header");
        return;
      }

      var accessToken = authHeader.ToString()["Bearer ".Length..].Trim();

      var resource = (ctx.ActionDescriptor.AttributeRouteInfo?.Template ?? request.Path).ToLowerInvariant();
      var scope = request.Method.ToLowerInvariant();

      var authorized = await keycloakPermissionService.AuthorizeAsync(accessToken, resource, scope);
      if (!authorized) {
        log.LogWarning("Unauthorized access to {Resource} with scope {Scope}", resource, scope);
        Deny(ctx, "Unauthorized Request");
      }
    }

    private static void Deny(AuthorizationFilterContext ctx, string reason) {
      var err = new JsonResult(new { success = false, error = reason });
      err.StatusCode = StatusCodes.Status401Unauthorized;
      ctx.Result = err;
    }

    /// <summary>Configurator</summary>
    public class Configurator : IConfigurator<IServiceCollection>, IConfigurator<IWebHostBuilder> {
      /// <inheritdoc/>
      public void AddTo(IServiceCollection svcColl, IConfiguration cfg) {
        svcColl.Configure<KeycloakConfig>(cfg.GetSection("config"));
        svcColl.AddSingleton<IKeycloakTokenService, KeycloakTokenService>();
        svcColl.AddSingleton<KeycloakAuthorizationFilter>();
        log.LogInformation("Service {s} added.", nameof(KeycloakAuthorizationFilter));
      }
      /// <inheritdoc/>
      public void AddTo(IWebHostBuilder hostBuilder, IConfiguration cfg)
        => hostBuilder.ConfigureServices(svcColl => AddTo(svcColl, cfg));
    }
  }
}