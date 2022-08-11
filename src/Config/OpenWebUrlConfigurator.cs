using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Linq;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Tlabs.Config {

  ///<summary>Open web url on startup configurator</summary>
  public class OpenWebUrlConfigurator : IConfigurator<MiddlewareContext> {
    static readonly ILogger log = Tlabs.App.Logger<OpenWebUrlConfigurator>();
    readonly IDictionary<string, string> config;
    string url;

    ///<summary>Default ctor.</summary>
    public OpenWebUrlConfigurator() : this(null) { }

    ///<summary>Ctor from <paramref name="config"/>.</summary>
    public OpenWebUrlConfigurator(IDictionary<string, string> config) {
      this.config= config ?? new Dictionary<string, string>(0);
    }
    ///<summary>Adds the open web url configuration to the <paramref name="mware"/>.</summary>
    public void AddTo(MiddlewareContext mware, IConfiguration cfg) {
      if (!config.TryGetValue("url", out this.url)) {
        /* Fall back to obtain url from 'webHosting.urls' setting:
          */
        var webSettings= App.Settings.GetSection(Tlabs.Server.ApplicationStartup.DFLT_HOST_SECTION);
        url= webSettings.GetValue<string>("urls", "http://localhost")
                        .Split(';').FirstOrDefault()   //'webHosting.urls' could specify multiple
                        ?.Replace("+", "localhost");
      }
      Tlabs.App.WithServiceScope(svcProv => {
        var appLifetime= svcProv.GetRequiredService<IHostApplicationLifetime>();
        appLifetime.ApplicationStarted.Register(onApplicationStarted);
        log.LogDebug("App.ContentRoot: {root}", App.ContentRoot);
        log.LogDebug("Registered for application startup event.");
      });
    }

    void onApplicationStarted() {
      log.LogDebug("On application startup.");

      if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        Process.Start(new ProcessStartInfo { FileName=url, UseShellExecute= true});   //Process.Start(new ProcessStartInfo("cmd", $"/c start {url}"));
      else log.LogWarning("Opening of {url} not supported on {desc}", url, RuntimeInformation.OSDescription);
    }
  }
}