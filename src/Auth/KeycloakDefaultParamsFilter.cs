using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Filters;

using Tlabs;
using Tlabs.Config;
using Tlabs.Server.Model;
using System.Net.Http;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc.Authorization;
using Tlabs.Data.Serialize.Json;

namespace Tlabs.Server.Auth
{
  ///<summary>Filter that </summary>
  public class KeycloakDefaultParamsFilter : IActionFilter
  {
    private static readonly ILogger log = Tlabs.App.Logger<KeycloakDefaultParamsFilter>();
    readonly IHttpClientFactory httpClientFactory;

    readonly KeycloakAuthorizationFilter.Options keycloakOptions;

    readonly IKeycloakTokenProvider keycloakTokenProvider;

    readonly IKeycloakResourceService keycloakResourceService;

    ///<summary>Ctor from <paramref name="httpClientFactory"/>. </summary>
    public KeycloakDefaultParamsFilter(IHttpClientFactory httpClientFactory, IKeycloakTokenProvider keycloakTokenProvider, IOptions<KeycloakAuthorizationFilter.Options> keycloakOptions, IKeycloakResourceService keycloakResourceService)
    {
      this.httpClientFactory = httpClientFactory;
      this.keycloakOptions = keycloakOptions.Value;
      this.keycloakTokenProvider = keycloakTokenProvider;
      this.keycloakResourceService = keycloakResourceService;
    }

    ///<inheritdoc/>
    public void OnActionExecuted(ActionExecutedContext context)
    {
      // Empty
    }

    ///<inheritdoc/>
    public void OnActionExecuting(ActionExecutingContext ctx)
    {
      // Skip filter if header does not contain an api key or action is marked as anonymous
      if (ctx.Filters.Any(item => item is IAllowAnonymousFilter)) { return; }
      var request = ctx.HttpContext.Request;

      request.Headers.TryGetValue("Authorization", out var authHeader);

      var accessToken = authHeader.ToString()["Bearer ".Length..].Trim();

      var saToken = keycloakTokenProvider.GetServiceAccountTokenAsync().GetAwaiter().GetResult(); // Get service account token

      var keycloakRequest = new HttpRequestMessage(
        HttpMethod.Post, $"{keycloakOptions.keycloakAuthority}/protocol/openid-connect/token")
      {
        Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
          ["grant_type"] = "urn:ietf:params:oauth:grant-type:uma-ticket",
          ["response_mode"] = "permissions",
          ["audience"] = keycloakOptions.keycloakAudience,
        })
      };

      keycloakRequest.Headers.Authorization =
              new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

      var response = httpClientFactory.CreateClient().SendAsync(keycloakRequest).GetAwaiter().GetResult();
      var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
      var seri = JsonFormat.CreateSerializer<List<KeycloakResourceService.KeycloakResourceListItem>>();

      var resources = seri.LoadObj(body);

      if (null == resources || 0 == resources.Count)
        return;
      
      List<Data.Model.Role.EnforcedParameter?> rawForcedParams = new();
      foreach (var res in resources)
      {
        var permissions = keycloakResourceService.GetResourceAttributesAsync(res.rsid).GetAwaiter().GetResult();
        if (null != permissions)
          foreach(var perm in permissions)
            rawForcedParams.Add(new Data.Model.Role.EnforcedParameter(perm));
      }
      var forcedParams = rawForcedParams.FirstOrDefault(x => x!.RouteRegex.Match(ctx.ActionDescriptor.AttributeRouteInfo?.Template?.ToLower(App.DfltFormat) ?? "").Success);


      if (null == forcedParams) return;

      // Get parameter name from role
      if (ctx.ActionDescriptor.Parameters.Count < forcedParams.Position)
      {
        log.LogError("No parameter with index {pos} found in controller action {name}", forcedParams.Position, ctx.ActionDescriptor.DisplayName);
        return;
      }

      var paramDesc = ctx.ActionDescriptor.Parameters[forcedParams.Position];

      foreach (var name in forcedParams.Values.Keys)
      {
        var value = forcedParams.Values[name];
        var param = ctx.ActionArguments[paramDesc.Name];

        if (param != null && param.GetType().IsGenericType && param.GetType().GetGenericTypeDefinition() == typeof(FilterParam<>))
        {
          var filterListProperty = param.GetType().GetProperty("FilterList");

          var filterListValue = filterListProperty?.GetValue(param) as List<Filter>;
          List<Filter> enforcedFilters = [.. filterListValue ?? []];

          var prop = enforcedFilters.FirstOrDefault(x => x.property == name);

          if (null != prop && null != prop.value)
          {
            // If user is filtering by this property with a different value, show him nothing
            prop.value = value.StartsWith(prop.value, StringComparison.OrdinalIgnoreCase) ? value : "#########";
          }
          else
          {
            enforcedFilters.Add(new Filter { property = name, value = value });
          }
          filterListProperty?.SetValue(param, enforcedFilters);
        }
      }
    }

    /// <summary>Configurator</summary>
    public class Configurator : IConfigurator<MiddlewareContext>, IConfigurator<IServiceCollection>
    {
      /// <inheritdoc/>
      public void AddTo(MiddlewareContext target, IConfiguration cfg)
      {
        Tlabs.App.WithServiceScope(svcProv =>
        {
          // Configure the ClockedRunner Operations - call ctor
          svcProv.GetRequiredService<KeycloakParamsSynchronizator>();
        });
      }
      /// <inheritdoc/>
      public void AddTo(IServiceCollection svcColl, IConfiguration cfg)
      {
        svcColl.AddSingleton<KeycloakDefaultParamsFilter>();
        svcColl.AddSingleton<IKeycloakTokenProvider, KeycloakTokenProvider>();
        svcColl.AddSingleton<IKeycloakResourceService, KeycloakResourceService>();
        svcColl.AddSingleton<KeycloakParamsSynchronizator>();
      }
      /// <inheritdoc/>
      public void AddTo(IWebHostBuilder hostBuilder, IConfiguration cfg)
        => hostBuilder.ConfigureServices(svcColl => AddTo(svcColl, cfg));
    }
  }
}