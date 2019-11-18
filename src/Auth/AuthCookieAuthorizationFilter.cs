using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tlabs.Config;
using Tlabs.Data.Entity;

namespace Tlabs.Server.Auth {
  /// <summary>Enforces the role authorization</summary>
  public class AuthCookieAuthorizationFilter : AuthorizeFilter {
    static readonly ILogger log= Tlabs.App.Logger<AuthCookieAuthorizationFilter>();

    /// <summary>Default ctor that requires auhtenticated user policy.</summary>
    public AuthCookieAuthorizationFilter() : base(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()) { }
    /// <inheritoc/>
    public AuthCookieAuthorizationFilter(AuthorizationPolicy policy) : base(policy) { }

    /// <inheritoc/>
    public AuthCookieAuthorizationFilter(IEnumerable<IAuthorizeData> authorizeData) : base(authorizeData) { }

    /// <inheritoc/>
    public AuthCookieAuthorizationFilter(string policy) : base(policy) { }

    /// <inheritoc/>
    public AuthCookieAuthorizationFilter(IAuthorizationPolicyProvider policyProvider, IEnumerable<IAuthorizeData> authorizeData) : base(policyProvider, authorizeData) { }

    /// <inheritoc/>
    public override async System.Threading.Tasks.Task OnAuthorizationAsync(AuthorizationFilterContext context) {
      if(!context.HttpContext.Request.Headers["Authorization"].Any()) {
        try {
          await base.OnAuthorizationAsync(context);

          var type= GetRole(context);

          if (null == type) return;

          var route= context.ActionDescriptor.AttributeRouteInfo.Template;

          var allowedActions= Role.DefaultAuthorizedActions[type.Value];
          var deniedActions= Role.DefaultDeniedActions[type.Value];

          bool denied= null != deniedActions && deniedActions.IsMatch(route);
          if (!denied && allowedActions.IsMatch(route)) return;

          context.Result= new ForbidResult(CookieAuthenticationDefaults.AuthenticationScheme);
        } catch (Exception e) {
          log.LogCritical("Error in authorization process: ", e);
          var err= new JsonResult(new {
            success= false,
            error= ""
          });
          err.StatusCode= StatusCodes.Status500InternalServerError;
          context.Result= err;  //setting a result does short-circuit the remainder of the filter pipeline...
        }
      }
    }

    /// <summary>Gets the current role based on the context</summary>
    public Role.RoleType? GetRole(AuthorizationFilterContext context) {
      var idSrvc= (Tlabs.Identity.IIdentityAccessor)App.ServiceProv.GetService(typeof(Tlabs.Identity.IIdentityAccessor));

      if (idSrvc==null) throw new ArgumentNullException(nameof(Tlabs.Identity.IIdentityAccessor));

      string currentRole= idSrvc.Roles.FirstOrDefault();

      if (currentRole==null) return null;

      return (Role.RoleType)Enum.Parse(typeof(Role.RoleType), currentRole);
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