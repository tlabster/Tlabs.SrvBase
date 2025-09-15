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

namespace Tlabs.Server.Auth
{
  ///<summary>Authorization filter for Keycloak.</summary>
  public class KeycloakAuthorizationFilter : IAsyncAuthorizationFilter
  {
    static readonly ILogger log = Tlabs.App.Logger<KeycloakAuthorizationFilter>();
    readonly Options authOptions;

    readonly IHttpClientFactory httpClientFactory;
    ///<summary>Ctor from <paramref name="options"/> and <paramref name="httpClientFactory"/>.</summary>
    public KeycloakAuthorizationFilter(
      IOptions<Options> options,
      IHttpClientFactory httpClientFactory)
    {
      this.authOptions = options.Value;
      this.httpClientFactory = httpClientFactory;
    }
    ///<inheritdoc/>
    public async Task OnAuthorizationAsync(AuthorizationFilterContext ctx)
    {
      // Skip filter if header does not contain an api key or action is marked as anonymous
      if (ctx.Filters.Any(item => item is IAllowAnonymousFilter)) { return; }
      var request = ctx.HttpContext.Request;

      // Extract Bearer token
      if (!request.Headers.TryGetValue("Authorization", out var authHeader) ||
          !authHeader.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
      {
        Deny(ctx, "Missing or invalid Authorization header");
        return;
      }

      var accessToken = authHeader.ToString()["Bearer ".Length..].Trim();

      // Build resource/scope dynamically (example: path+HTTP method)
      var resource = ctx.ActionDescriptor.AttributeRouteInfo?.Template ?? request.Path;
      var scope = request.Method.ToLowerInvariant();

      //var saToken = await tokenProvider.GetServiceAccountTokenAsync(); // Get service account token

      var client = httpClientFactory.CreateClient();

      var keycloakRequest = new HttpRequestMessage(HttpMethod.Post,
            $"{authOptions.keycloakAuthority}/protocol/openid-connect/token")
      {
        Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
          ["grant_type"] = "urn:ietf:params:oauth:grant-type:uma-ticket",
          ["response_mode"] = "decision",
          ["audience"] = authOptions.keycloakAudience,
        })
      };

      keycloakRequest.Headers.Authorization =
              new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

      var response = await client.SendAsync(keycloakRequest);
      var body = await response.Content.ReadAsStringAsync();

      if (!response.IsSuccessStatusCode || !body.Contains("\"result\":true"))
      {
        log.LogWarning("Unauthorized access to {Resource} with scope {Scope}", resource, scope);
        Deny(ctx, "Unauthorized Request");
      }
    }

    private static void Deny(AuthorizationFilterContext ctx, string reason)
    {
      var err = new JsonResult(new { success = false, error = reason });
      err.StatusCode = StatusCodes.Status403Forbidden;
      ctx.Result = err;
    }

    ///<summary>Filter options.</summary>
    public class Options
    {
      ///<summary>Keycloak authority URL.</summary>
      public string? keycloakAuthority { get; set; }
      public string? keycloakAuth { get; set; }
      ///<summary>Audience for Keycloak tokens.</summary>
      public string? keycloakAudience { get; set; }
      public string? clientId { get; set; }
      public string? clientSecret { get; set; }
    }
    /// <summary>Configurator</summary>
    public class Configurator : IConfigurator<IServiceCollection>, IConfigurator<IWebHostBuilder>
    {
      /// <inheritdoc/>
      public void AddTo(IServiceCollection svcColl, IConfiguration cfg)
      {
        svcColl.Configure<Options>(cfg.GetSection("config"));
        // svcColl.AddSingleton<IKeycloakTokenProvider, KeycloakTokenProvider>();
        svcColl.AddSingleton<KeycloakAuthorizationFilter>();
        log.LogInformation("Service {s} added.", nameof(KeycloakAuthorizationFilter));
      }
      /// <inheritdoc/>
      public void AddTo(IWebHostBuilder hostBuilder, IConfiguration cfg)
        => hostBuilder.ConfigureServices(svcColl => AddTo(svcColl, cfg));
    }
  }
}