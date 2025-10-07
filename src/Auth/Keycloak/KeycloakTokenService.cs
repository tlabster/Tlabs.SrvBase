using System;
using System.Collections.Generic;
using System.Linq.Dynamic.Core.Tokenizer;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.Extensions.Options;

using Tlabs.Data.Serialize.Json;

namespace Tlabs.Server.Auth.Keycloak {
  /// <summary>Keycloak client service</summary>
  public interface IKeycloakTokenService {
    /// <summary>Gets the authorized resources for a given <paramref name="accessToken"/></summary>
    Task<IReadOnlyList<KeycloakResourceListItem>> GetUserResourcesAsync(string accessToken);

    /// <summary>Check if <paramref name="accessToken"/> is authorized for <paramref name="resource"/> in <paramref name="scope"/></summary>
    Task<bool> AuthorizeAsync(string accessToken, string resource, string scope);

    /// <summary> Refresh token corresponding to <paramref name="refreshToken"/></summary>
    Task<TokenResponse> RefreshTokensAsync(string refreshToken);
  }

  /// <summary>Implementation of a keycloak API Service</summary>
  public class KeycloakTokenService : IKeycloakTokenService {
    private readonly IHttpClientFactory httpClientFactory;
    private readonly KeycloakConfig keycloakOptions;
    private readonly string tokenEndpoint;

    /// <summary>Constructor from <paramref name="httpClientFactory"/> and <paramref name="keycloakOptions"/></summary>
    public KeycloakTokenService(
      IHttpClientFactory httpClientFactory,
      IOptions<KeycloakConfig> keycloakOptions
    ) {
      this.httpClientFactory = httpClientFactory;
      this.keycloakOptions = keycloakOptions.Value;
      this.tokenEndpoint = $"{this.keycloakOptions.Client.Authority}/protocol/openid-connect/token";
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<KeycloakResourceListItem>> GetUserResourcesAsync(string accessToken) {
      var keycloakRequest = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint) {
        Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
          ["grant_type"] = "urn:ietf:params:oauth:grant-type:uma-ticket",
          ["response_mode"] = "permissions",
          ["audience"] = keycloakOptions.Client.Audience,
        })
      };

      var header = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
      keycloakRequest.Headers.Authorization = header;

      var client = httpClientFactory.CreateClient();
      var response = await client.SendAsync(keycloakRequest);
      response.EnsureSuccessStatusCode();

      var body = await response.Content.ReadAsStringAsync();

      var seri = JsonFormat.CreateSerializer<List<KeycloakResourceListItem>>();
      var resources = seri.LoadObj(body) ?? new List<KeycloakResourceListItem>();

      return resources;
    }

    /// <inheritdoc/>
    public async Task<bool> AuthorizeAsync(string accessToken, string resource, string scope) {
      var keycloakRequest = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint) {
        Content = new FormUrlEncodedContent(new Dictionary<string, string> {
          ["grant_type"] = "urn:ietf:params:oauth:grant-type:uma-ticket",
          ["response_mode"] = "decision",
          ["permission"] = $"{resource}#{scope}",
          ["permission_resource_format"] = "uri",
          ["permission_resource_matching_uri"] = "true",
          ["audience"] = keycloakOptions.Client.Audience,
        })
      };

      keycloakRequest.Headers.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

      var client = httpClientFactory.CreateClient();
      var response = await client.SendAsync(keycloakRequest);

      if (!response.IsSuccessStatusCode) return false;

      var body = await response.Content.ReadAsStringAsync();
      return body.Contains("\"result\":true", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Refresh the token with <paramref name="refreshToken"/></summary>
    public async Task<TokenResponse> RefreshTokensAsync(string refreshToken) {
      var content = new FormUrlEncodedContent(new Dictionary<string, string> {
            { "client_id", keycloakOptions.Client.ClientId! },
            { "client_secret", keycloakOptions.Client.ClientSecret },
            { "grant_type", "refresh_token" },
            { "refresh_token", refreshToken }
        });

      var client = httpClientFactory.CreateClient();
      var response = await client.PostAsync(tokenEndpoint, content);
      response.EnsureSuccessStatusCode();

      var seri = JsonFormat.CreateSerializer<Dictionary<string, IConvertible>>();
      var body = await response.Content.ReadAsStringAsync();
      var resources = seri.LoadObj(body) ?? new Dictionary<string, IConvertible>();

      var tokenResponse = new TokenResponse();

      if (resources.TryGetValue("access_token", out var accessToken)) {
        tokenResponse.AccessToken = Convert.ToString(accessToken);
      }

      if (resources.TryGetValue("refresh_token", out var newRefreshToken)) {
        tokenResponse.RefreshToken = Convert.ToString(newRefreshToken);
      }

      if (resources.TryGetValue("expires_in", out var expiresIn)) {
        tokenResponse.ExpiresAt = DateTime.UtcNow.AddSeconds(Convert.ToInt32(expiresIn) - 30); // safety margin
      }

      return tokenResponse;
    }
  }
}
