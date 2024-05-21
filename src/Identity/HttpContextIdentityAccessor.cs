using System;
using System.Security.Claims;

using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Tlabs.Identity {

  ///<summary>Accessor retuning the current identity registered with the HttpContext.</summary>
  public class HttpContextIdentityAccessor : SysIdentityAccessor {
    static readonly ILogger<HttpContextIdentityAccessor> log= App.Logger<HttpContextIdentityAccessor>();
    readonly IHttpContextAccessor httpCtx;
    readonly IdentityOptions identOpt;
    ///<summary>Ctor from <paramref name="httpCtx"/>.</summary>
    public HttpContextIdentityAccessor(IHttpContextAccessor httpCtx, IOptions<IdentityOptions> optAcc) {
      this.httpCtx= httpCtx;
      this.identOpt= optAcc.Value ?? new IdentityOptions();
    }
    ///<inheritdoc/>
    public override ClaimsPrincipal Principal {
      get {
        var userIdentity= httpCtx.HttpContext?.User;
        if (null != userIdentity)
          return userIdentity;
        log.LogDebug("No user identity found with current context - using default system identity.");
        return sysPrincipal;
      }
    }
    ///<inheritdoc/>
    public override string? Name => Principal.Identity?.Name;
    ///<inheritdoc/>
    public override string? AuthenticationType => Principal.Identity?.AuthenticationType;
    ///<inheritdoc/>
    public override int Id {
      get {
#pragma warning disable CA1806  // use default id value
        Int32.TryParse(Principal.FindFirstValue(identOpt.ClaimsIdentity.UserIdClaimType) ?? "0", out var id);
#pragma warning restore CA1806
        return id;
      }
    }
    ///<inheritdoc/>
    public override string[] Roles {
      get {
        var role= Principal.FindFirstValue(identOpt.ClaimsIdentity.RoleClaimType);
        return null != role ? new string[] { role } : Array.Empty<string>();
      }
    }
  }
}