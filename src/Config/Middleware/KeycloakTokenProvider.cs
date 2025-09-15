

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Tlabs.Server.Auth
{
  public interface IKeycloakTokenProvider
  {
    Task<string> GetServiceAccountTokenAsync();
  }

  public class KeycloakTokenProvider : IKeycloakTokenProvider
  {
    private readonly IHttpClientFactory httpClientFactory;
    private readonly KeycloakAuthorizationFilter.Options keycloakOptions;
    private readonly SemaphoreSlim sLock = new(1, 1);

    private string? cachedToken;
    private DateTime expiresAt;

    public KeycloakTokenProvider(IHttpClientFactory httpClientFactory, IOptions<KeycloakAuthorizationFilter.Options> options)
    {
      this.httpClientFactory = httpClientFactory;
      this.keycloakOptions = options.Value;
    }

    public async Task<string> GetServiceAccountTokenAsync()
    {
      if (!string.IsNullOrEmpty(cachedToken) && DateTime.UtcNow < expiresAt)
        return cachedToken;

      await sLock.WaitAsync();
      try
      {
        if (!string.IsNullOrEmpty(cachedToken) && DateTime.UtcNow < expiresAt)
          return cachedToken;

        var client = httpClientFactory.CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Post,
            $"{keycloakOptions.keycloakAuthority}/protocol/openid-connect/token")
        {
          Content = new FormUrlEncodedContent(new Dictionary<string, string>
          {
            ["grant_type"] = "client_credentials",
            ["client_id"] = keycloakOptions.clientId,
            ["client_secret"] = keycloakOptions.clientSecret
          })
        };

        var resp = await client.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var token = json!.GetProperty("access_token").GetString();
        var expiresIn = json.GetProperty("expires_in").GetInt32();

        cachedToken = token;
        expiresAt = DateTime.UtcNow.AddSeconds(expiresIn - 30);
        return cachedToken!;
      }
      finally
      {
        sLock.Release();
      }
    }
  }
}