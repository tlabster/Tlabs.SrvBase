using System;
using System.Security.Claims;

using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Options;

using Tlabs.Identity;

namespace Tlabs.Server.Identity {

  ///<summary>Accessor retuning the current identity registered with the HttpContext.</summary>
  public class HttpContextIdentityAccessor : SysIdentityAccessor {
    private ILogger<HttpContextIdentityAccessor> log= App.Logger<HttpContextIdentityAccessor>();
    private IHttpContextAccessor httpCtx;
    private IdentityOptions identOpt;
    ///<summary>Ctor from <paramref name="httpCtx"/>.</summary>
    public HttpContextIdentityAccessor(IHttpContextAccessor httpCtx, IOptions<IdentityOptions> optAcc) {
      this.httpCtx= httpCtx;
      this.identOpt= optAcc.Value ?? new IdentityOptions();
    }
    ///<inherit/>
    public override ClaimsPrincipal Principal {
      get {
        var userIdentity= httpCtx.HttpContext?.User;
        if (null != userIdentity)
          return userIdentity;
        log.LogDebug("No user identity found with current context - using default system identity.");
        return sysPrincipal;
      }
    }
    ///<inherit/>
    public override string Name => Principal.Identity.Name;
    ///<inherit/>
    public override string AuthenticationType => Principal.Identity.AuthenticationType;
    ///<inherit/>
    public override int Id {
      get {
        int id= 0;
        Int32.TryParse(Principal.FindFirstValue(identOpt.ClaimsIdentity.UserIdClaimType) ?? "0", out id);
        return id;
      }
    }
    ///<inherit/>
    public override string[] Roles {
      get {
        return new string[] { Principal.FindFirstValue(identOpt.ClaimsIdentity.RoleClaimType) };
      }
    }
  }
}