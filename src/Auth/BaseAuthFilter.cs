using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Tlabs.Data;
using Tlabs.Data.Entity;
using Tlabs.Identity;

namespace Tlabs.Server.Auth {
  ///<summary>Enforces the role authorization</summary>
  public class BaseAuthFilter : IAuthorizationFilter {
    ///<summary>Logger</summary>
    protected static readonly ILogger log= Tlabs.App.Logger<BaseAuthFilter>();

    ///<summary>Key used by the token auth mechanism</summary>
    protected const string HEADER_AUTH_KEY= "Authorization";
    readonly IRolesAdministration rolesAdm;
    ///<summary>Ctor from <paramref name="rolesAdm"/>.</summary>
    public BaseAuthFilter(IRolesAdministration rolesAdm) => this.rolesAdm= rolesAdm;

    ///<summary>Defaults to forbidden if no other filter allows</summary>
    public virtual void OnAuthorization(AuthorizationFilterContext context) {
      setUnauthorized(context);
    }

    internal static string? ParseAuthorizationKey(Microsoft.Extensions.Primitives.StringValues authorize) {
      string? key= null;
      if (1 != authorize.Count) return key;
      var authParts= authorize[0]?.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
      if (2 == authParts?.Length && string.Equals(authParts[0].Trim(), "ApiKey", StringComparison.OrdinalIgnoreCase))
        key= authParts[1];
      return key;
    }

    ///<summary>Checks if the current request is allowed for anonymous</summary>
    protected bool isAnonymous(AuthorizationFilterContext context) {
      /* When doing endpoint routing, MVC does not add AllowAnonymousFilters for AllowAnonymousAttributes that
        * were discovered on controllers and actions.
        * As a workaround we check for the presence of IAllowAnonymous in endpoint metadata.
        * (https://docs.microsoft.com/en-us/dotnet/core/compatibility/aspnetcore#authorization-iallowanonymous-removed-from-authorizationfiltercontextfilters)
        * Skip filter if header is marked as anonymous or apiKey was provided and filter did not short circuit the pipeline
        */
      var endPoint= context.HttpContext.GetEndpoint();
      return null != endPoint?.Metadata?.GetMetadata<IAllowAnonymous>();
    }

    ///<summary>Checks if any of the given roles has access to the current URL</summary>
    protected bool checkRoles(IEnumerable<string>? currentRoles, AuthorizationFilterContext context) {
      if (null != currentRoles) {
        var route= context.ActionDescriptor.AttributeRouteInfo?.Template?.ToLower(App.DfltFormat) ?? "";
        var method= context.HttpContext.Request.Method.ToUpper(App.DfltFormat);
        var roles= currentRoles.Select(r => rolesAdm.GetByName(r));
        if (roles.Any(role => role.AllowsAction(method, route))) return true;
      }

      setUnauthorized(context);
      return false;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0051:Remove unused private member", Justification = "Might be needed in future")]
    static Role? loadRole(string name) {
      Role? role= null;
      App.WithServiceScope(prov => {
        var roleRepo= (IRepo<Role>)prov.GetRequiredService(typeof(IRepo<Role>));
        role= roleRepo.AllUntracked.Single(x => x.Name == name);
      });
      return role;
    }

    ///<summary>Set result to forbidden</summary>
    protected void setForbidden(AuthorizationFilterContext ctx) {
      if(isAnonymous(ctx)) return;
      log.LogInformation("Forbidden access: {path}", ctx.HttpContext.Request.Path);

      var err= new JsonResult(new {
        success= false,
        error= "Forbidden Request"
      });
      err.StatusCode= StatusCodes.Status403Forbidden;
      ctx.Result= err;  //setting a result does short-circuit the remainder of the filter pipeline...
    }

    ///<summary>Set result to unauthorized</summary>
    protected void setUnauthorized(AuthorizationFilterContext ctx) {
      if(isAnonymous(ctx)) return;
      log.LogInformation("Unauthorized access: {path}", ctx.HttpContext.Request.Path);
      var err= new JsonResult(new {
        success= false,
        error= "Unauthorized Request"
      });
      err.StatusCode= StatusCodes.Status401Unauthorized;
      ctx.Result= err;  //setting a result does short-circuit the remainder of the filter pipeline...
    }

    ///<summary>Set result to error</summary>
    protected void setError(AuthorizationFilterContext ctx, Exception e) {
      log.LogCritical(e, "Error in authorization process");
      var err= new JsonResult(new {
        success= false,
        error= ""
      });
      err.StatusCode= StatusCodes.Status500InternalServerError;
      ctx.Result= err;
    }
  }
}