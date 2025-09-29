using System;
using System.Collections.Generic;

namespace Tlabs.Server.Auth.Keycloak {
  /// <summary>
  /// Reponse containing a new access token
  /// </summary>
  public record TokenResponse {
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public string? AccessToken { get; set; } = "";
    public string? RefreshToken { get; set; } = "";
    public DateTime? ExpiresAt { get; set; }
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
  }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

  /// <summary>Keycloak resource list item as returned by the Keycloak API</summary>
  public class KeycloakResourceListItem {
    /// <summary>
    /// The resource name
    /// </summary>
    public string rsname { get; set; } = "";
    /// <summary>
    /// The resource ID
    /// </summary>
    public string rsid { get; set; } = "";
    /// <summary>
    /// The resource scopes
    /// </summary>
    public List<string> scopes { get; set; } = new();
  }
}