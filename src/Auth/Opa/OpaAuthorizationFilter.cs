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
using Microsoft.AspNetCore.Server.HttpSys;
using System.Security.Claims;
using Tlabs.Server.Model;

namespace Tlabs.Server.Auth.Opa {
  ///<summary>Authorization filter for Open Policy Agent.</summary>
  public class OpaAuthorizationFilter : IAsyncAuthorizationFilter {
    static readonly ILogger log = Tlabs.App.Logger<OpaAuthorizationFilter>();
    private readonly IOpaClient opaClient;

    ///<summary>Ctor.</summary>
    public OpaAuthorizationFilter(IOpaClient opaClient) {
      this.opaClient = opaClient;
    }

    ///<inheritdoc/>
    public async Task OnAuthorizationAsync(AuthorizationFilterContext ctx) {
      if (ctx.HttpContext.IsAnonymous()) return;
      if (ctx.HttpContext.User.Identity?.IsAuthenticated == false) {
        Deny(ctx, "Unauthorized request", StatusCodes.Status403Forbidden);
        return;
      }

      var request = ctx.HttpContext.Request;

      var actionName = ctx.ActionDescriptor.DisplayName;
      var path = (ctx.ActionDescriptor.AttributeRouteInfo?.Template ?? request.Path).ToLowerInvariant();

      var input = new OpaInput {
        Resource = actionName,
        Path = path.Split("/"),
        Method = request.Method,
        Subject = new Subject {
          User = ctx.HttpContext.User.Identity?.Name ?? "anonymous",
          Roles = ctx.HttpContext.User.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToArray()
        },
      };

      var cancellationToken = ctx.HttpContext.RequestAborted;
      var authorized = await opaClient.EvaluateDecisionAsync<string>(input, cancellationToken);
      if (!authorized.Allow) {
        log.LogWarning("Unauthorized access to {Resource} with scope {Scope}", actionName, request.Method);
        Deny(ctx, "Forbidden access", StatusCodes.Status403Forbidden);
      }

      if (authorized.Constraints != null) {
        ctx.HttpContext.Items["OpaEnforcedFilter"] = authorized.Constraints;
      }
    }

    private static void Deny(AuthorizationFilterContext ctx, string reason, int? code = StatusCodes.Status401Unauthorized) {
      var err = new JsonResult(new { success = false, error = reason });
      err.StatusCode = code;
      ctx.Result = err;
    }

    /// <summary>Configures the OpaAuthorizationFilter</summary>
    /// <remarks>This configurator also configures the OpaClient</remarks>
    public class Configurator : IConfigurator<IServiceCollection>, IConfigurator<IWebHostBuilder> {
      /// <inheritdoc/>
      public void AddTo(IServiceCollection svcColl, IConfiguration cfg) {
        var config = cfg.GetSection("config");
        svcColl.Configure<OpaClientConfig>(config);
        var uri = config["OpaUri"]?.TrimEnd('/') ?? "http://localhost:8181";
        svcColl.AddHttpClient("Opa", httpClient => {
          httpClient.BaseAddress = new Uri(uri);
        });
        svcColl.AddSingleton<IOpaClient, OpaClient>();

        svcColl.AddSingleton<OpaAuthorizationFilter>();
        svcColl.AddSingleton<OpaDefaultParamsFilter>();

        log.LogInformation("Service {s} added.", nameof(OpaAuthorizationFilter));
      }
      /// <inheritdoc/>
      public void AddTo(IWebHostBuilder hostBuilder, IConfiguration cfg)
        => hostBuilder.ConfigureServices(svcColl => AddTo(svcColl, cfg));
    }
  }
}