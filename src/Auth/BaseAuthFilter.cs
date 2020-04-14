using System;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Tlabs.Data;
using Tlabs.Data.Entity;

namespace Tlabs.Server.Auth {
  ///<summary>Enforces the role authorization</summary>
  public class BaseAuthFilter : IAuthorizationFilter {
    ///<summary>Logger</summary>
    protected static readonly ILogger log= Tlabs.App.Logger<BaseAuthFilter>();

    ///<summary>Key used by the token auth mechanism</summary>
    protected const string HEADER_AUTH_KEY= "Authorization";

    ///<summary>Defaults to forbidden if no other filter allows</summary>
    public virtual void OnAuthorization(AuthorizationFilterContext context) {
      unauthorized(context);
    }

    ///<summary>Checks if any of the given roles has access to the current URL</summary>
    protected bool checkRoles(string[] currentRoles, AuthorizationFilterContext context) {
      if (!currentRoles.Any()) {
        unauthorized(context);
        return false;
      }

      var route= context.ActionDescriptor.AttributeRouteInfo.Template.ToLower();
      var method= context.HttpContext.Request.Method.ToUpper();
      var roles= currentRoles.Select(x => Role.Cache[x, loadRole(x)]);

      if (roles.Any(role => role.AllowsAction(method, route))) return true;
      return false;
    }

    private Role loadRole(string name) {
      Role role= null;
      App.WithServiceScope(prov => {
        var roleRepo= (IRepo<Role>)prov.GetService(typeof(IRepo<Role>));
        role= roleRepo.AllUntracked.Single(x => x.Name == name);
      });
      return role;
    }

    ///<summary>Set result to forbidden</summary>
    protected void forbidden(AuthorizationFilterContext ctx) {
      log.LogInformation("Forbidden access: {path}", ctx.HttpContext.Request.Path);

      var err= new JsonResult(new {
        success= false,
        error= "Unauthorized Request"
      });
      err.StatusCode= StatusCodes.Status401Unauthorized;
      ctx.Result= err;  //setting a result does short-circuit the remainder of the filter pipeline...
    }

    ///<summary>Set result to unauthorized</summary>
    protected void unauthorized(AuthorizationFilterContext ctx) {
      log.LogInformation("Unauthorized access: {path}", ctx.HttpContext.Request.Path);
      var err= new JsonResult(new {
        success= false,
        error= "Unauthorized Request"
      });
      err.StatusCode= StatusCodes.Status401Unauthorized;
      ctx.Result= err;  //setting a result does short-circuit the remainder of the filter pipeline...
    }

    ///<summary>Set result to error</summary>
    protected void errorResult(AuthorizationFilterContext ctx, Exception e) {
      log.LogCritical("Error in authorization process: ", e);
      var err= new JsonResult(new {
        success= false,
        error= ""
      });
      err.StatusCode= StatusCodes.Status500InternalServerError;
      ctx.Result= err;
    }
  }
}