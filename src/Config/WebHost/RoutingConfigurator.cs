using System;
using System.Collections.Generic;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Json;


namespace Tlabs.Config {

  ///<summary>Configures routing services to <see cref="IServiceCollection"/>.</summary>
  ///<remarks>Use this configurator to setup the routing for minimal API w/o MVC controllers.</remarks>
  public class RoutingConfigurator : IConfigurator<IWebHostBuilder> {
    ///<summary>Use regex config name</summary>
    public const string USE_REGEX_CFG= "userRegex";
    static readonly ILogger log= App.Logger<RoutingConfigurator>();
    readonly IDictionary<string, string> config;

    ///<summary>Default ctor.</summary>
    public RoutingConfigurator() : this(null) { }

    ///<summary>Ctor from <paramref name="config"/>.</summary>
    public RoutingConfigurator(IDictionary<string, string>? config) {
      this.config= config ?? new Dictionary<string, string>();
    }

    ///<inheritdoc/>
    public void AddTo(IWebHostBuilder webHostBuilder, IConfiguration cfg) =>
      webHostBuilder.ConfigureServices(services => {
        var svc=   config.TryGetValue(USE_REGEX_CFG, out var useRegex) && Boolean.TryParse(useRegex, out var use) && use
                 ? services.AddRouting()
                 : services.AddRoutingCore();
        svc.AddEndpointsApiExplorer();
        svc.Configure<JsonOptions>(opt => {
          if (config.TryGetValue("formatting", out var frmt))
            opt.SerializerOptions.WriteIndented= frmt.Equals("Indented", StringComparison.OrdinalIgnoreCase);
        });
        log.LogInformation("API routing configured");
      });

  }
}