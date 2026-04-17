using System.ComponentModel.DataAnnotations;
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
      var pathBase = cfg.GetSection("config").Get<PathBaseOptions>()?.PathBase;
      appBuilder.UsePathBase(pathBase);
      log.LogInformation("Path base middleware configured");
    }
  }
  /// <summary>
  /// Options for Path Base middleware
  /// </summary>
  public class PathBaseOptions {
    /// <summary>
    /// The base path to use for the application
    /// </summary>
    [Required]
    public string PathBase { get; set; } = "/";
  }
}