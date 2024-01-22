
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.FileProviders;

namespace Tlabs.Config {

  ///<summary>Configures static file middleware.</summary>
  public class StaticContentConfigurator : IConfigurator<MiddlewareContext> {
    readonly ILogger log;
    readonly IDictionary<string, string> config;
    ///<summary>Default ctor.</summary>
    public StaticContentConfigurator() : this(null) { }

    ///<summary>Ctor from <paramref name="config"/>.</summary>
    public StaticContentConfigurator(IDictionary<string, string>? config) {
      this.config= config ?? new Dictionary<string, string>(0);
      this.log= App.Logger<StaticContentConfigurator>();
    }
    ///<inheritdoc/>
    public void AddTo(MiddlewareContext mware, IConfiguration cfg) {

      /* Configure default page(s):
       * (This MUST come before UseStaticFiles()...)
       */
      if (config.TryGetValue("defaultPage", out var dfltPage) && !string.IsNullOrEmpty(dfltPage)) {
        string[] dfltPages = dfltPage.Split(',');
        log.LogInformation("Default page(s) are: '{defaultPages}'", string.Join(",", dfltPages));
        mware.AppBuilder.UseDefaultFiles(new DefaultFilesOptions() {
          DefaultFileNames= dfltPages
        });
      }

      mware.AppBuilder.UseStaticFiles(); //from HostingEnv.WebRootPath
      log.LogInformation("Serving static content from: '{webroot}'", mware.HostingEnv.WebRootPath);

      foreach (var pair in config.Where(p => p.Key.StartsWith('/'))) {
        var contPath = Path.Combine(App.ContentRoot, pair.Value);
        if (!Directory.Exists(contPath)) {
          log.LogDebug("Ignoring non exisiting static content path: {path}", contPath);
          continue; //UseStaticFiles would throw on non exisiting path
        }
        mware.AppBuilder.UseStaticFiles(new StaticFileOptions() {
          RequestPath= new PathString(pair.Key),
          FileProvider= new PhysicalFileProvider(contPath)
        });
        log.LogInformation("Serving static content from {path}: '{physical}'", pair.Key, pair.Value);
      }
    }
  }
}