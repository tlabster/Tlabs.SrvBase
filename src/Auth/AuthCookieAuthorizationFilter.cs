using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Microsoft.AspNetCore.Hosting;
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
        if(isAnonymous(context) || 0 != context.HttpContext.Request.Headers[HEADER_AUTH_KEY].Count) return;

        var idSrvc= (Tlabs.Identity.IIdentityAccessor)App.ServiceProv.GetRequiredService(typeof(Tlabs.Identity.IIdentityAccessor));
        if (idSrvc.Name == null) {
          setUnauthorized(context);
          return;
        }
        if (checkRoles(idSrvc.Roles, context)) return;
        setForbidden(context);
      }
      catch (Exception e) {
        log.LogCritical(e, "Error in authorization process: ");
        setError(context, e);
      }
    }

    /// <summary>Configurator</summary>
    public class Configurator : IConfigurator<IServiceCollection>, IConfigurator<IWebHostBuilder> {
      /// <inheritoc/>
      public void AddTo(IServiceCollection svcColl, IConfiguration cfg) {
        var log= App.Logger<Configurator>();
        svcColl.AddSingleton<AuthCookieAuthorizationFilter>();
        log.LogInformation("Service {s} added.", nameof(AuthCookieAuthorizationFilter));
      }
      /// <inheritdoc/>
      public void AddTo(IWebHostBuilder hostBuilder, IConfiguration cfg)
        => hostBuilder.ConfigureServices(svcColl => AddTo(svcColl, cfg));
    }
  }
}