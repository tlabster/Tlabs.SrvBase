using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;

using Tlabs.Data.Entity;

namespace Tlabs.Config {
  /// <summary>Enforces the role authorization</summary>
  public class BriefTemplAuthorizationFilter : AuthorizeFilter {
    /// <inherit/>
    public BriefTemplAuthorizationFilter(AuthorizationPolicy policy) : base(policy) { }

    /// <inherit/>
    public BriefTemplAuthorizationFilter(IEnumerable<IAuthorizeData> authorizeData) : base(authorizeData) { }

    /// <inherit/>
    public BriefTemplAuthorizationFilter(string policy) : base(policy) { }

    /// <inherit/>
    public BriefTemplAuthorizationFilter(IAuthorizationPolicyProvider policyProvider, IEnumerable<IAuthorizeData> authorizeData) : base(policyProvider, authorizeData) { }

    /// <inherit/>
    public override async System.Threading.Tasks.Task OnAuthorizationAsync(AuthorizationFilterContext context) {
      await base.OnAuthorizationAsync(context);

      var type= GetRole(context);

      if (null == type) return;

      var route= context.ActionDescriptor.AttributeRouteInfo.Template;

      var allowedActions= Role.DefaultAuthorizedActions[type.Value];
      var deniedActions= Role.DefaultDeniedActions[type.Value];

      bool denied= null != deniedActions && deniedActions.IsMatch(route);
      if (!denied && allowedActions.IsMatch(route)) return;

      context.Result= new ForbidResult(CookieAuthenticationDefaults.AuthenticationScheme);
    }

    /// <summary>Gets the current role based on the context</summary>
    public Role.RoleType? GetRole(AuthorizationFilterContext context) {
      var idSrvc= (Identity.IIdentityAccessor)App.ServiceProv.GetService(typeof(Identity.IIdentityAccessor));

      if (idSrvc==null) throw new ArgumentNullException(nameof(Identity.IIdentityAccessor));

      string currentRole= idSrvc.Roles.FirstOrDefault();

      if (currentRole==null) return null;

      return (Role.RoleType)Enum.Parse(typeof(Role.RoleType), currentRole);
    }
  }
}