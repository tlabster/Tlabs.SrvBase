using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Tlabs.Config {
  /// <summary>
  /// Configurator for Path Base middleware
  /// </summary>
  public class PathBaseConfigurator : IConfigurator<MiddlewareContext> {
    readonly ILogger log = App.Logger<PathBaseConfigurator>();

    ///<inheritdoc/>
    public void AddTo(MiddlewareContext mware, IConfiguration cfg) {
      var appBuilder = mware.AppBuilder;
      appBuilder.UsePathBase(cfg["config:pathBase"]);
      log.LogInformation("Path base middleware configured");
    }
  }
}