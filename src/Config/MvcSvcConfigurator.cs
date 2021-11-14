using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;


using Tlabs.Data.Serialize.Json;

namespace Tlabs.Config {

  ///<summary>Configures MVC to <see cref="IServiceCollection"/>>.</summary>
  public class MvcSvcConfigurator : IConfigurator<IServiceCollection> {
    IDictionary<string, string> config;

    ///<summary>Default ctor.</summary>
    public MvcSvcConfigurator() : this(null) { }

    ///<summary>Ctor from <paramref name="config"/>.</summary>
    public MvcSvcConfigurator(IDictionary<string, string> config) {
      this.config= config ?? new Dictionary<string, string>();
    }

    ///<inherit/>
    public void AddTo(IServiceCollection services, IConfiguration cfg) {
      var log= App.Logger<MvcSvcConfigurator>();

      // Add ASP.NET MVC framework services.
      services.AddControllers(opt => {
        var filterKeys= config.Keys.Where(k => k.StartsWith("filter") || k.IndexOf("_filter") >=0).OrderBy(k => k);
        foreach(var filter in filterKeys) {
          var typeName= config[filter];
          if (!string.IsNullOrEmpty(typeName)) {
            opt.Filters.AddService(Misc.Safe.LoadType(typeName, filter));
            log.LogInformation("MVC {f} ({t}) added.", filter, typeName);
          }
        }
      }).AddJsonOptions(configureJsonOptions)
        .AddApplicationPart(Assembly.GetEntryAssembly());

      log.LogInformation("ASP.NET MVC framework services added.");
    }

    private void configureJsonOptions(JsonOptions opt) {
      JsonFormat.ApplyDefaultOptions(opt.JsonSerializerOptions);

      string frmt;
      if (config.TryGetValue("formatting", out frmt))
        opt.JsonSerializerOptions.WriteIndented= frmt.Equals("Indented", StringComparison.OrdinalIgnoreCase);
    }
  }
}