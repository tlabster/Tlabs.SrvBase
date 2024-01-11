using System;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
        if(isAnonymous(context) || context.HttpContext.Request.Headers[HEADER_AUTH_KEY].Any()) return;

        var idSrvc= (Tlabs.Identity.IIdentityAccessor)App.ServiceProv.GetService(typeof(Tlabs.Identity.IIdentityAccessor));
        if (idSrvc.Name == null) {
          unauthorized(context);
          return;
        }
        if (checkRoles(idSrvc.Roles, context)) return;
        forbidden(context);
      }
      catch (Exception e) {
        log.LogCritical(e, "Error in authorization process: ");
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