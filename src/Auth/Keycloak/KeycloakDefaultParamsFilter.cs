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
using System.Threading.Tasks;

namespace Tlabs.Server.Auth.Keycloak {
  ///<summary>Filter that applies default action parameters based on the authenticated user</summary>
  public class KeycloakDefaultParamsFilter : IAsyncActionFilter {
    private static readonly ILogger log = Tlabs.App.Logger<KeycloakDefaultParamsFilter>();
    readonly IHttpClientFactory httpClientFactory;
    readonly KeycloakAuthorizationFilter.Options keycloakOptions;
    readonly IKeycloakTokenProvider keycloakTokenProvider;
    readonly IKeycloakResourceService keycloakResourceService;
    readonly IKeycloakTokenService keycloakPermissionService;

    ///<summary>Ctor from <paramref name="httpClientFactory"/>. </summary>
    public KeycloakDefaultParamsFilter(
      IHttpClientFactory httpClientFactory,
      IKeycloakTokenProvider keycloakTokenProvider,
      IOptions<KeycloakAuthorizationFilter.Options> keycloakOptions,
      IKeycloakResourceService keycloakResourceService,
      IKeycloakTokenService keycloakPermissionService
    ) {
      this.httpClientFactory = httpClientFactory;
      this.keycloakOptions = keycloakOptions.Value;
      this.keycloakTokenProvider = keycloakTokenProvider;
      this.keycloakResourceService = keycloakResourceService;
      this.keycloakPermissionService = keycloakPermissionService;
    }

    ///<inheritdoc/>
    public async Task OnActionExecutionAsync(ActionExecutingContext ctx, ActionExecutionDelegate next) {
      await ApplyFilterParamsAsync(ctx);
      await next();
      // Nothing after action execution
    }

    private async Task ApplyFilterParamsAsync(ActionExecutingContext ctx) {
      // Skip filter if header does not contain an api key or action is marked as anonymous
      if (ctx.Filters.Any(item => item is IAllowAnonymousFilter)) return;

      var request = ctx.HttpContext.Request;

      request.Headers.TryGetValue("Authorization", out var authHeader);

      var accessToken = authHeader.ToString()["Bearer ".Length..].Trim();

      var resources = await keycloakPermissionService.GetUserResourcesAsync(accessToken);

      if (null == resources || 0 == resources.Count)
        return;

      List<Data.Model.Role.EnforcedParameter?> rawForcedParams = new();
      foreach (var res in resources) {
        var permissions = keycloakResourceService.GetResourceAttributesAsync(res.rsid).GetAwaiter().GetResult();
        if (null != permissions)
          foreach (var perm in permissions)
            rawForcedParams.Add(new Data.Model.Role.EnforcedParameter(perm));
      }
      var forcedParams = rawForcedParams.FirstOrDefault(x => x!.RouteRegex.Match(ctx.ActionDescriptor.AttributeRouteInfo?.Template?.ToLower(App.DfltFormat) ?? "").Success);

      if (null == forcedParams) return;

      // Get parameter name from role
      if (ctx.ActionDescriptor.Parameters.Count < forcedParams.Position) {
        log.LogError("No parameter with index {pos} found in controller action {name}", forcedParams.Position, ctx.ActionDescriptor.DisplayName);
        return;
      }

      var paramDesc = ctx.ActionDescriptor.Parameters[forcedParams.Position];

      foreach (var name in forcedParams.Values.Keys) {
        var value = forcedParams.Values[name];
        var param = ctx.ActionArguments[paramDesc.Name];

        if (param != null && param.GetType().IsGenericType && param.GetType().GetGenericTypeDefinition() == typeof(FilterParam<>)) {
          var filterListProperty = param.GetType().GetProperty("FilterList");

          var filterListValue = filterListProperty?.GetValue(param) as List<Filter>;
          List<Filter> enforcedFilters = [.. filterListValue ?? []];

          var prop = enforcedFilters.FirstOrDefault(x => x.property == name);

          if (null != prop && null != prop.value) {
            // If user is filtering by this property with a different value, show him nothing
            prop.value = value.StartsWith(prop.value, StringComparison.OrdinalIgnoreCase) ? value : "#########";
          }
          else {
            enforcedFilters.Add(new Filter { property = name, value = value });
          }
          filterListProperty?.SetValue(param, enforcedFilters);
        }
      }
    }

    /// <summary>Configurator</summary>
    public class Configurator : IConfigurator<MiddlewareContext>, IConfigurator<IServiceCollection> {
      /// <inheritdoc/>
      public void AddTo(MiddlewareContext target, IConfiguration cfg) {
        Tlabs.App.WithServiceScope(svcProv => {
          // Configure the ClockedRunner Operations - call ctor
          svcProv.GetRequiredService<KeycloakParamsSynchronizator>();
        });
      }
      /// <inheritdoc/>
      public void AddTo(IServiceCollection svcColl, IConfiguration cfg) {
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