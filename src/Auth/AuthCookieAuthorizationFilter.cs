using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tlabs.Config;
using Microsoft.AspNetCore.Mvc.Authorization;

namespace Tlabs.Server.Auth {
  /// <summary>Enforces the role authorization</summary>
  public class AuthCookieAuthorizationFilter : BaseAuthFilter {

    /// <inheritoc/>
    public override void OnAuthorization(AuthorizationFilterContext context) {
      try {
        // Skip filter if header is marked as anonymous or apiKey was provided and filter did not short circuit the pipeline
        if (context.Filters.Any(item => item is IAllowAnonymousFilter) || context.HttpContext.Request.Headers[HEADER_AUTH_KEY].Any()) {
          return;
        }

        var idSrvc= (Tlabs.Identity.IIdentityAccessor)App.ServiceProv.GetService(typeof(Tlabs.Identity.IIdentityAccessor));
        if (idSrvc.Name == null) {
          unauthorized(context);
          return;
        }
        if (checkRoles(idSrvc.Roles, context)) return;
        forbidden(context);
      }
      catch (Exception e) {
        log.LogCritical("Error in authorization process: ", e);
        errorResult(context, e);
      }
    }

    /// <summary>Configurator</summary>
    public class Configurator : IConfigurator<IServiceCollection> {
      /// <inheritoc/>
      public void AddTo(IServiceCollection svcColl, IConfiguration cfg) {
        var log= App.Logger<Configurator>();
        svcColl.AddSingleton(new AuthCookieAuthorizationFilter());
        log.LogInformation("Service {s} added.", nameof(AuthCookieAuthorizationFilter));
      }
    }
  }
}