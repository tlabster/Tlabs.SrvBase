using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Tlabs.Data.Serialize.Json;

namespace Tlabs.Server.Auth.Keycloak {
  /// <summary>
  /// Service for caching and retrieving Keycloak resource information
  /// </summary>
  public interface IKeycloakResourceService {
    /// <summary>Get resource attributes for a specific resource ID</summary>
    /// <param name="resourceId">The Keycloak resource ID</param>
    /// <returns>List of enforced filter attributes for the resource</returns>
    Task<List<string>> GetResourceAttributesAsync(string resourceId);

    /// <summary>Refresh of the resource cache</summary>
    Task RefreshCacheAsync(CancellationToken ctk = default);
  }

  /// <summary>Background service that caches Keycloak resources and provides access to resource attributes</summary>
  public class KeycloakResourceService : IKeycloakResourceService {
    private static readonly ILogger log = Tlabs.App.Logger<KeycloakResourceService>();
    private readonly KeycloakAuthorizationFilter.Options keycloakOptions;
    private readonly IKeycloakTokenProvider keycloakTokenProvider;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly ConcurrentDictionary<string, List<string>> resourceAttributesCache = new();

    /// <summary>Constructor</summary>
    public KeycloakResourceService(
      IKeycloakTokenProvider keycloakTokenProvider,
      IHttpClientFactory httpClientFactory,
      IOptions<KeycloakAuthorizationFilter.Options> keycloakOptions
    ) {
      this.keycloakOptions = keycloakOptions.Value;
      this.keycloakTokenProvider = keycloakTokenProvider;
      this.httpClientFactory = httpClientFactory;
    }

    /// <inheritdoc/>
    public async Task<List<string>> GetResourceAttributesAsync(string resourceId) {
      if (resourceAttributesCache.TryGetValue(resourceId, out var cached)) {
        return cached ?? new List<string>();
      }

      // If not in cache, refresh cache immediately
      log.LogInformation("Resource {resourceId} not found in cache, refreshing immediately", resourceId);
      await RefreshCacheAsync();
      return resourceAttributesCache.TryGetValue(resourceId, out var fetched) ? fetched : new List<string>();
    }

    /// <inheritdoc/>
    public async Task RefreshCacheAsync(CancellationToken ctk = default) {
      ctk.ThrowIfCancellationRequested();

      var serviceAccountToken = await keycloakTokenProvider.GetServiceAccountTokenAsync();

      var client = httpClientFactory.CreateClient("keycloak-protection");

      // Get all resources uids using the Keycloak API
      var resourceListRequest = new HttpRequestMessage(HttpMethod.Get, $"{keycloakOptions.KeycloakAuthority}/authz/protection/resource_set");
      resourceListRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", serviceAccountToken);

      var resourceListResponse = await client.SendAsync(resourceListRequest, ctk);
      if (!resourceListResponse.IsSuccessStatusCode) {
        log.LogError("Failed to fetch resource list from Keycloak API: {statusCode}", resourceListResponse.StatusCode);
        return;
      }

      var resourceListBody = await resourceListResponse.Content.ReadAsStringAsync(ctk);
      var seri = JsonFormat.CreateSerializer<List<string>>();
      var resources = seri.LoadObj(resourceListBody);

      if (resources == null || resources.Count==0) return;

      foreach (var rsid in resources) {
        resourceAttributesCache[rsid] = await FetchResourceAttributesAsync(rsid, serviceAccountToken);
      }
    }

    /// <summary>
    /// Fetches resource attributes for a single resource
    /// </summary>
    private async Task<List<string>> FetchResourceAttributesAsync(string resourceId, string? accessToken = null) {

      accessToken ??= await keycloakTokenProvider.GetServiceAccountTokenAsync();

      var client = httpClientFactory.CreateClient("keycloak-protection");
      var keycloakRequest = new HttpRequestMessage(HttpMethod.Get,
        $"{keycloakOptions.KeycloakAuthority}/authz/protection/resource_set/{resourceId}");
      keycloakRequest.Headers.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

      var response = await client.SendAsync(keycloakRequest);
      if (!response.IsSuccessStatusCode) {
        log.LogError("Failed to fetch resource {resourceId}: {statusCode}", resourceId, response.StatusCode);
        return new List<string>();
      }

      var body = await response.Content.ReadAsStringAsync();
      var seri = JsonFormat.CreateSerializer<KeycloakResource>();
      var resource = seri.LoadObj(body);

      var enforcedFilters = resource?.attributes?.TryGetValue("enforcedFilters", out var filters) ?? false
        ? filters
        : new List<string>();

      return enforcedFilters;
    }

    private class KeycloakResource {
      public string _id { get; set; } = "";
      public string name { get; set; } = "";
      public string displayName { get; set; } = "";
      public Dictionary<string, List<string>>? attributes { get; set; }
      public string type { get; set; } = "";
      public bool ownerManagedAccess { get; set; }
      public List<string> uris { get; set; } = new();
      public List<Scope> resource_scopes { get; set; } = new();
      public Dictionary<string, string> owner { get; set; } = new();
      public List<Scope> scopes { get; set; } = new();
      public string icon_uri { get; set; } = "";

      public class Scope {
        public string name { get; set; } = "";
      }
    }
  }
}
