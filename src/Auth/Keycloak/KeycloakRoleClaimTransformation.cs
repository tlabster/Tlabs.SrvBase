using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Authentication;

using Tlabs.Data.Serialize.Json;

namespace Tlabs.Server.Auth.Keycloak {
  /// <summary>
  /// Transform keycloak roles into a role claim
  /// </summary>
  public class KeycloakRolesClaimsTransformation : IClaimsTransformation {
    /// <inheritdoc/>
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal) {
      var identity = (ClaimsIdentity)principal.Identity!;
      var realmAccessClaim = identity.FindFirst("realm_access");
      if (realmAccessClaim != null) {
        var seri = JsonFormat.CreateSerializer<Dictionary<string, IEnumerable<string>>>();
        var roles = seri.LoadObj(realmAccessClaim.Value) ?? new();
        if (roles.TryGetValue("roles", out var rolesElement)) {
          foreach (var role in rolesElement) {
            identity.AddClaim(new Claim(identity.RoleClaimType, role));
          }
        }
      }
      return Task.FromResult(principal);
    }
  }
}