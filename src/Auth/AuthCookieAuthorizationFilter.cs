using System;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;

using Tlabs.Config;
using Tlabs.Identity;

namespace Tlabs.Server.Auth {
  /// <summary>Enforces the role authorization</summary>
  public class AuthCookieAuthorizationFilter : BaseAuthFilter {
    /// <summary>Ctor form <paramref name="rolesAdm"/>.</summary>
    public AuthCookieAuthorizationFilter(IRolesAdministration rolesAdm) : base(rolesAdm) { }
    
    /// <inheritoc/>
    public override void OnAuthorization(AuthorizationFilterContext context) {
      try {
        /* When doing endpoint routing, MVC does not add AllowAnonymousFilters for AllowAnonymousAttributes that
         * were discovered on controllers and actions.
         * As a workaround we check for the presence of IAllowAnonymous in endpoint metadata.
         * (https://docs.microsoft.com/en-us/dotnet/core/compatibility/aspnetcore#authorization-iallowanonymous-removed-from-authorizationfiltercontextfilters)
         * Skip filter if header is marked as anonymous or apiKey was provided and filter did not short circuit the pipeline
         */
        var endPoint= context.HttpContext.GetEndpoint();
        if (null != (endPoint?.Metadata?.GetMetadata<IAllowAnonymous>()) || context.HttpContext.Request.Headers[HEADER_AUTH_KEY].Any()) {
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
        svcColl.AddSingleton<AuthCookieAuthorizationFilter>();
        log.LogInformation("Service {s} added.", nameof(AuthCookieAuthorizationFilter));
      }
    }
  }
}