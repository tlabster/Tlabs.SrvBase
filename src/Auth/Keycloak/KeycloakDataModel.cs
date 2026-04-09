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
    public string Rsname { get; set; } = "";
    /// <summary>
    /// The resource ID
    /// </summary>
    public string Rsid { get; set; } = "";
    /// <summary>
    /// The resource scopes
    /// </summary>
    public List<string> Scopes { get; set; } = new();
  }
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
  public class KeycloakClientConfig {
    public string BaseUrl { get; set; } = "";
    public string Realm { get; set; } = "";
    public string Authority { get { return $"{BaseUrl}/realms/{Realm}"; } }
    public string Audience { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string LogoutRedirect { get; set; } = "";
    public bool RequireHttpsMetadata { get; set; } = true;
    public int IdleLogoutMinutes { get; set; } = 30;
  }

  public class KeycloakConfig {
    public KeycloakClientConfig Client { get; set; } = new();
    public int SyncInterval { get; set; } = 300; //seconds
  }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}