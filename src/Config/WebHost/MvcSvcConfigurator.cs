using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;


using Tlabs.Data.Serialize.Json;

namespace Tlabs.Config {

  ///<summary>Configures MVC to <see cref="IServiceCollection"/>>.</summary>
  ///<remarks>Includes the setup for minimal API routing.</remarks>
  public class MvcSvcConfigurator : IConfigurator<IServiceCollection>, IConfigurator<IWebHostBuilder> {
    ///<summary>Additional MVC options.</summary>
    public class Options {
      ///<summary>List of assemblies to be searched for controller classes.</summary>
      public string[]? applicationParts { get; set; }
    }

    static readonly ILogger log= App.Logger<MvcSvcConfigurator>();
    readonly IDictionary<string, string> config;

    ///<summary>Default ctor.</summary>
    public MvcSvcConfigurator() : this(null) { }

    ///<summary>Ctor from <paramref name="config"/>.</summary>
    public MvcSvcConfigurator(IDictionary<string, string>? config) {
      this.config= config ?? new Dictionary<string, string>();
    }

    ///<inheritdoc/>
    public void AddTo(IServiceCollection services, IConfiguration cfg) => configureMvcServices(services, cfg);

    void configureMvcServices(IServiceCollection services, IConfiguration cfg) {
      services.AddRouting();
      services.AddEndpointsApiExplorer();

      // Add ASP.NET MVC framework services.
      var mvcBuilder= services.AddControllers(opt => {
        var filterKeys= config.Keys.Where(k => k.StartsWith("filter", StringComparison.Ordinal) || k.Contains("_filter", StringComparison.Ordinal)).OrderBy(k => k);
        foreach (var filter in filterKeys) {
          var typeName= config[filter];
          if (!string.IsNullOrEmpty(typeName)) {
            opt.Filters.AddService(Misc.Safe.LoadType(typeName, filter));
            log.LogInformation("MVC {f} ({t}) added.", filter, typeName);
          }
        }
      }).AddJsonOptions(configureJsonOptions)
        .AddApplicationPart(Assembly.GetEntryAssembly()!);

      var options= cfg.GetSection("options").Get<Options>();
      if (null != options?.applicationParts) foreach (var asmName in options.applicationParts) {
        mvcBuilder.AddApplicationPart(Assembly.Load(asmName));
        log.LogInformation("App. part added from: {part}", asmName);
      }

      /* Global JSON serializer options:
       */
      services.ConfigureHttpJsonOptions(opt => {
        JsonFormat.ApplyDefaultOptions(opt.SerializerOptions);
        if (config.TryGetValue("formatting", out var frmt))
          opt.SerializerOptions.WriteIndented= frmt.Equals("Indented", StringComparison.OrdinalIgnoreCase);
      });

      log.LogInformation("ASP.NET MVC framework services added.");
    }
    ///<inheritdoc/>
    public void AddTo(IWebHostBuilder webHostBuilder, IConfiguration cfg) =>
      webHostBuilder.ConfigureServices(services => configureMvcServices(services, cfg));

    private void configureJsonOptions(JsonOptions opt) {
      JsonFormat.ApplyDefaultOptions(opt.JsonSerializerOptions);

      if (config.TryGetValue("formatting", out var frmt))
        opt.JsonSerializerOptions.WriteIndented= frmt.Equals("Indented", StringComparison.OrdinalIgnoreCase);
    }
  }
}