using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HostFiltering;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Tlabs.Config;

namespace Tlabs.Server {

  ///<summary>Class to facillitate the creation of the application's start-up configuration expressed as a pre-configured <see cref="IHostBuilder"/>.</summary>
  ///<remarks>The ambition of this <see cref="ApplicationStartup"/> class is to reducee hard-coded pre-configuration to an absolute minimum with favor of
  ///leveraging the <see cref="IConfigurator{T}"/> base <c>ApplyConfigurator(...)</c> pattern.
  ///</remarks>
  public sealed class ApplicationStartup {
    ///<summary>Default section of the configuratiion to be used for the start-up hosting environment setup.</summary>
    public const string DFLT_HOST_SECTION= "webHosting";
    const string APP_MIDDLEWARE_SECTION= "applicationMiddleware";
    static readonly string[] ANY_HOST= new[] { "*" };

    ///<summary>Create a <see cref="IHostBuilder"/> from optional command line <paramref name="args"/>.</summary>
    public static IHostBuilder CreateServerHostBuilder(string[]? args= null) => CreateServerHostBuilder(DFLT_HOST_SECTION, args);
    ///<summary>Create a <see cref="IHostBuilder"/> from <paramref name="hostSection"/> and command line <paramref name="args"/>.</summary>
    public static IHostBuilder CreateServerHostBuilder(string hostSection, string[]? args= null) {

      var hostBuilder= Tlabs.ApplicationStartup.CreateAppHostBuilder(hostSection, args, (hostBuilder, hostSettings) => {
        /* This prepares the configuration of the actual Microsoft.AspNetCore.Hosting.Server.IServer to be hosted as as a IHostedService...
         * (Typically this ends up to configure a Kestrel server, when ApplyConfiguration(...) is run, but still depends on the actual 'confgurator' section of the hostSettings)
         */
        hostBuilder.ConfigureWebHost(webBuilder => {  //ConfigureWebHostDefaults(cfg => {
                                                      //https://github.com/dotnet/aspnetcore/blob/c925f99cddac0df90ed0bc4a07ecda6b054a0b02/src/DefaultBuilder/src/GenericHostBuilderExtensions.cs
                                                      //https://github.com/dotnet/aspnetcore/blob/c925f99cddac0df90ed0bc4a07ecda6b054a0b02/src/DefaultBuilder/src/WebHost.cs

          webBuilder.UseConfiguration(hostSettings);  //<- use properties from hostSettings (properties defined in https://github.com/dotnet/aspnetcore/blob/main/src/Hosting/Hosting/src/Internal/WebHostOptions.cs )
          webBuilder.UseStartup<ApplicationStartup>();
          webBuilder.UseSetting(HostDefaults.ApplicationKey, Assembly.GetEntryAssembly()?.GetName().Name); //fix assembly name being set by UseStartup<>...
          configureDefaultHostfiltering(webBuilder);
          webBuilder.ConfigureServices((WebHostBuilderContext hostingContext, IServiceCollection services) => {
            services.AddRouting();
          });
          webBuilder.ApplyConfigurators(hostSettings, "configurator"); //configure the actual server
        });
      });

      return hostBuilder;
    }

    static void configureDefaultHostfiltering(IWebHostBuilder web) {
      web.ConfigureServices((hostingContext, services) => {
        // Fallback
        services.PostConfigure<HostFilteringOptions>(options => {
          if (options.AllowedHosts == null || options.AllowedHosts.Count == 0) {
            // "AllowedHosts": "localhost;127.0.0.1;[::1]"
            var hosts= hostingContext.Configuration["AllowedHosts"]?.Split(';' , StringSplitOptions.RemoveEmptyEntries);
            // Fall back to "*" to disable.
            options.AllowedHosts= (hosts?.Length > 0 ? hosts : ANY_HOST);
          }
        });
        // Change notification
        services.AddSingleton<IOptionsChangeTokenSource<HostFilteringOptions>>(
                    new ConfigurationChangeTokenSource<HostFilteringOptions>(hostingContext.Configuration));

        // see ServerStartup.Configure()   // services.AddTransient<IStartupFilter, HostFilteringStartupFilter>();

      });
    }

    static bool isForwardedHeaders;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:not used", Justification= "Possible futute use")]
    static void configureHeaderForwarding(IWebHostBuilder web) {
      web.ConfigureServices((hostingContext, services) => {
        isForwardedHeaders= string.Equals("true", hostingContext.Configuration["ForwardedHeaders_Enabled"], StringComparison.OrdinalIgnoreCase);
        if (!isForwardedHeaders) return;
        services.Configure<ForwardedHeadersOptions>(options => {
          options.ForwardedHeaders= ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
          // Only loopback proxies are allowed by default. Clear that restriction because forwarders are
          // being enabled by explicit configuration.
          options.KnownNetworks.Clear();
          options.KnownProxies.Clear();
        });

          // see ServerStartup.Configure()   //services.AddTransient<IStartupFilter, ForwardedHeadersStartupFilter>();
      });
    }

    readonly IWebHostEnvironment env;

    ///<summary>Ctor taking <paramref name="env"/>.</summary>
    public ApplicationStartup(IWebHostEnvironment env) {
      this.env= env;
      App.Logger<ApplicationStartup>().LogInformation("{appName} starting with ContentPath= '{contentPath}'\n\tWebRootPath= '{webRootPath}'", env.ApplicationName, env.ContentRootPath, env.WebRootPath);
    }

    ///<summary>Configure application service provider container.</summary>
    ///<remarks>This method gets called by the runtime before calling Configure().</remarks>
    public void ConfigureServices(IServiceCollection services) { }

    ///<summary>Configure the application middleware (HTTP request pipeline).</summary>
    ///<remarks>This method gets called by the runtime after services have been configured with ConfigureServices().</remarks>
    public void Configure(IApplicationBuilder app) {
      App.Setup= App.Setup with { ServiceProv= app.ApplicationServices };
      App.AppLifetime.ApplicationStopped.Register(() => {
        App.Logger<ApplicationStartup>().LogCritical("Shutdown.\n\n");
        Serilog.Log.CloseAndFlush();
      });

      app.UseHostFiltering(); // Should be first in the pipeline
      if (isForwardedHeaders) app.UseForwardedHeaders();

      new MiddlewareContext() {
        HostingEnv= this.env,
        AppBuilder= app
      }.ApplyConfigurators(App.Settings, APP_MIDDLEWARE_SECTION);
    }

  }
}
