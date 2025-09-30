using System;
using System.Threading;

using Microsoft.Extensions.Options;

using Tlabs.Timing;

namespace Tlabs.Server.Auth.Keycloak {
  /// <summary>
  /// Background service that periodically refreshes Keycloak resource parameters
  /// to keep the local cache up to date.
  /// </summary>
  public class KeycloakParamsSynchronizator : IDisposable {
    private readonly KeycloakConfig keycloakOptions;
    IKeycloakResourceService keycloakResourceService;
    readonly ClockedRunner? sendClk;
    /// <summary>Constructor</summary>
    public KeycloakParamsSynchronizator(IOptions<KeycloakConfig> options, IKeycloakResourceService keycloakResourceService) {
      this.keycloakResourceService = keycloakResourceService;
      this.keycloakOptions = options.Value;

      if (keycloakOptions.SyncInterval > 0) {
        sendClk = new("Keycloak resources sync job", keycloakOptions.SyncInterval * 1000, RefreshKeycloakResources, Tlabs.App.AppLifetime.ApplicationStopping);
      }
    }

    bool RefreshKeycloakResources(CancellationToken ctk) {
      keycloakResourceService.RefreshCacheAsync(ctk).GetAwaiter().GetResult();
      return ctk.IsCancellationRequested;
    }

    /// <inheritdoc/>
    public void Dispose() {
      Dispose(true);
      GC.SuppressFinalize(this);
    }

    /// <inheritdoc/>
    protected virtual void Dispose(bool disposing) {
      if (disposing && sendClk != null) {
        sendClk.Dispose();
      }
    }
  }
}