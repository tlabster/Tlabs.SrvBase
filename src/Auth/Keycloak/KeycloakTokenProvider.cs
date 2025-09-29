

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Options;

using Tlabs.Data.Serialize.Json;

namespace Tlabs.Server.Auth.Keycloak {
  /// <summary>
  /// Service for obtaining and caching Keycloak service account token
  /// </summary>
  public interface IKeycloakTokenProvider {
    /// <summary>Get Keycloak service account token</summary>
    Task<string> GetServiceAccountTokenAsync();
  }
  /// <inheritdoc/>
  public class KeycloakTokenProvider : IKeycloakTokenProvider {
    private readonly IHttpClientFactory httpClientFactory;
    private readonly KeycloakAuthorizationFilter.Options keycloakOptions;
    private readonly SemaphoreSlim sLock = new(1, 1);

    private string? cachedToken;
    private DateTime expiresAt;

    /// <summary>Ctor</summary>
    public KeycloakTokenProvider(IHttpClientFactory httpClientFactory, IOptions<KeycloakAuthorizationFilter.Options> options) {
      this.httpClientFactory = httpClientFactory;
      this.keycloakOptions = options.Value;
    }
    /// <inheritdoc/>
    public async Task<string> GetServiceAccountTokenAsync() {
      if (keycloakOptions == null)
        throw new InvalidOperationException("Keycloak options are not configured.");

      if (!string.IsNullOrEmpty(cachedToken) && DateTime.UtcNow < expiresAt)
        return cachedToken;

      await sLock.WaitAsync();
      try {
        if (!string.IsNullOrEmpty(cachedToken) && DateTime.UtcNow < expiresAt)
          return cachedToken;

        var client = httpClientFactory.CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Post,
            $"{keycloakOptions.KeycloakAuthority}/protocol/openid-connect/token")
        {
          Content = new FormUrlEncodedContent(new Dictionary<string, string>
          {
            ["grant_type"] = "client_credentials",
            ["client_id"] = keycloakOptions.ClientId,
            ["client_secret"] = keycloakOptions.ClientSecret
          })
        };

        var resp = await client.SendAsync(req);
        resp.EnsureSuccessStatusCode();

        var seri = JsonFormat.CreateSerializer<TokenResponse>();

        var tokenObj = seri.LoadObj(await resp.Content.ReadAsStringAsync());

        if (tokenObj != null) {
          cachedToken = tokenObj.access_token;
          expiresAt = DateTime.UtcNow.AddSeconds(tokenObj.expires_in - 30);
          return cachedToken!;
        }
        throw new InvalidOperationException("Failed to obtain access token from Keycloak.");
      }
      finally {
        sLock.Release();
      }
    }

    private class TokenResponse {
      public string access_token { get; set; } = "";
      public int expires_in { get; set; }
      public int refresh_expires_in { get; set; }
      public string token_type { get; set; } = "";
      public string scope { get; set; } = "";
    }
  }
}