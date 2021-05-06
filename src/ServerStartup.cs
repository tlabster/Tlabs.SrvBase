using System;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HostFiltering;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tlabs.Config;

namespace Tlabs.Server {
  using RTinfo= System.Runtime.InteropServices.RuntimeInformation;

  ///<summary>Class to facillitate the creation of the application's start-up configuration expressed as a pre-configured <see cref="IHostBuilder"/>.</summary>
  ///<remarks>The ambition of this <see cref="ApplicationStartup"/> class is to reducee hard-coded pre-configuration to an absolute minimum with favor of
  ///leveraging the <see cref="IConfigurator{T}"/> base <c>ApplyConfigurator(...)</c> pattern.
  ///</remarks>
  public sealed class ApplicationStartup {
    ///<summary>Default section of the configuratiion to be used for the start-up hosting environment setup.</summary>
    public const string DFLT_HOST_SECTION= "webHosting";
    const string APP_SVC_SECTION= "applicationServices";
    const string APP_MIDDLEWARE_SECTION= "applicationMiddleware";
    const string ENV_DOTNET_PFX= "DOTNET_";
    const string ENV_ASPNET_PFX= "ASPNET_";
    static Assembly entryAsm= Assembly.GetEntryAssembly();
    static ILogger log;

    static ILoggerFactory logFactory;

    ///<summary>Create a <see cref="IHostBuilder"/> from optional command line <paramref name="args"/>.</summary>
    public static IHostBuilder CreateServerHostBuilder(string[] args= null) => CreateServerHostBuilder(DFLT_HOST_SECTION, args);
    ///<summary>Create a <see cref="IHostBuilder"/> from <paramref name="hostSection"/> and command line <paramref name="args"/>.</summary>
    public static IHostBuilder CreateServerHostBuilder(string hostSection, string[] args= null) {

      var hostSettings= App.Settings.GetSection(hostSection)
                                    .ToConfigurationBuilder()
                                    .AddCommandLine(args)
                                    .Build();
      /* Create the logging facillity (ILogFactory based)
       * for being availble immediately (even before the DI service provider has been setup...)
       * from App.Logger<T>
       */
      logFactory= createLogFactory(hostSettings);
      ApplicationStartup.log= App.Logger<ApplicationStartup>();             //startup logger


      var hostBuilder= new HostBuilder(); //Host.CreateDefaultBuilder(args) [https://github.com/dotnet/runtime/blob/79ae74f5ca5c8a6fe3a48935e85bd7374959c570/src/libraries/Microsoft.Extensions.Hosting/src/Host.cs]
      hostBuilder.UseContentRoot(App.ContentRoot);
      hostBuilder.ConfigureHostConfiguration(host => host.AddEnvironmentVariables(prefix: ENV_DOTNET_PFX));
      hostBuilder.ConfigureAppConfiguration((hostingCtx, config) => config.AddEnvironmentVariables(prefix: ENV_ASPNET_PFX));

      /* This prepares the configuration of the actual Microsoft.AspNetCore.Hosting.Server.IServer to be hosted as as a IHostedService...
       * (Typically this ends up to configure a Kestrel server, when ApplyConfiguration(...) is run, but still depends on the actual 'confgurator' section of the hostSettings)
       */
      hostBuilder.ConfigureWebHost(webBuilder => {  //ConfigureWebHostDefaults(cfg => {
                                                    //https://github.com/dotnet/aspnetcore/blob/c925f99cddac0df90ed0bc4a07ecda6b054a0b02/src/DefaultBuilder/src/GenericHostBuilderExtensions.cs
                                                    //https://github.com/dotnet/aspnetcore/blob/c925f99cddac0df90ed0bc4a07ecda6b054a0b02/src/DefaultBuilder/src/WebHost.cs
        webBuilder.UseConfiguration(hostSettings);
        webBuilder.UseStartup<ApplicationStartup>();
        webBuilder.UseSetting(HostDefaults.ApplicationKey, Assembly.GetEntryAssembly().GetName().Name); //fix assembly name being set by UseStartup<>...
        configureDefaultHostfiltering(webBuilder);
        webBuilder.ConfigureServices((hostingContext, services) => {
          services.AddRouting();
        });
        webBuilder.ApplyConfigurators(hostSettings, "configurator"); //configure the actual server
      });
      
      /* Configure DI service provider (with validation in development environment).
       */
      hostBuilder.UseDefaultServiceProvider((hostingCtx, options) => {
          bool isDevelopment= hostingCtx.HostingEnvironment.IsDevelopment();
          options.ValidateScopes= isDevelopment;
          options.ValidateOnBuild= isDevelopment;
      });

      /* Add the logFactory as service:
       */
      hostBuilder.ConfigureServices((hostingCtx, services) => {
        services.AddOptions();

        services.AddSingleton<ILoggerFactory>(logFactory);
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IConfigureOptions<LoggerFilterOptions>>(
              new DefaultLoggerLevelConfigureOptions(LogLevel.Information)));         
      });
        
      return hostBuilder;
    }

    static ILoggerFactory createLogFactory(IConfigurationRoot config) {
      var logConfig= App.Settings.GetSection("logging");
      var logFac= LoggerFactory.Create(log => {
        log.AddConfiguration(logConfig);
        log.AddEventSourceLogger();
        log.ApplyConfigurators(logConfig, "configurator");
        log.Services.Configure<LoggerFactoryOptions>(opt => opt.ActivityTrackingOptions=   ActivityTrackingOptions.SpanId
                                                                                         | ActivityTrackingOptions.TraceId
                                                                                         | ActivityTrackingOptions.ParentId
        );
      });
      App.LogFactory= logFac;
      App.Logger<ApplicationStartup>().LogCritical(        //this is the very first log entry
        "*** {appName}\n" +
        "\t({path})\n" +
        "\ton {netVers} ({arch})\n" +
        "\t - {os}",
        entryAsm.FullName,
        entryAsm.Location,
        $"{RTinfo.FrameworkDescription} framwork", RTinfo.OSArchitecture,
        RTinfo.OSDescription);
      return logFac;
    }

    sealed class DefaultLoggerLevelConfigureOptions : ConfigureOptions<LoggerFilterOptions> {
      public DefaultLoggerLevelConfigureOptions(LogLevel level) : base(options => options.MinLevel = level) { }
    }

    //This was a default of CreateDefaultBuilder() consider to 
    static void configureUserSecret(IHostEnvironment env, IConfigurationBuilder config) {
      if (env.IsDevelopment() && !string.IsNullOrEmpty(env.ApplicationName)) {
        var appAssembly = Assembly.Load(new AssemblyName(env.ApplicationName));
        if (appAssembly != null) {
          config.AddUserSecrets(appAssembly, optional: true);
        }
      }
    }

    static void configureDefaultHostfiltering(IWebHostBuilder web) {
      web.ConfigureServices((hostingContext, services) => {
        // Fallback
        services.PostConfigure<HostFilteringOptions>(options => {
          if (options.AllowedHosts == null || options.AllowedHosts.Count == 0) {
            // "AllowedHosts": "localhost;127.0.0.1;[::1]"
            var hosts = hostingContext.Configuration["AllowedHosts"]?.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            // Fall back to "*" to disable.
            options.AllowedHosts = (hosts?.Length > 0 ? hosts : new[] { "*" });
          }
        });
        // Change notification
        services.AddSingleton<IOptionsChangeTokenSource<HostFilteringOptions>>(
                    new ConfigurationChangeTokenSource<HostFilteringOptions>(hostingContext.Configuration));

        // see ServerStartup.Configure()   // services.AddTransient<IStartupFilter, HostFilteringStartupFilter>();

      });
    }

    static bool isForwardedHeaders;
    static void configureHeaderForwarding(IWebHostBuilder web) {
      web.ConfigureServices((hostingContext, services) => {
        isForwardedHeaders= string.Equals("true", hostingContext.Configuration["ForwardedHeaders_Enabled"], StringComparison.OrdinalIgnoreCase);
        if (!isForwardedHeaders) return;
        services.Configure<ForwardedHeadersOptions>(options => {
          options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
          // Only loopback proxies are allowed by default. Clear that restriction because forwarders are
          // being enabled by explicit configuration.
          options.KnownNetworks.Clear();
          options.KnownProxies.Clear();
        });

          // see ServerStartup.Configure()   //services.AddTransient<IStartupFilter, ForwardedHeadersStartupFilter>();
      });
    }

    private static void onShutdown() {
      App.Logger<ApplicationStartup>().LogCritical("Shutdown.\n\n");
      Serilog.Log.CloseAndFlush();
    }

    IWebHostEnvironment env;

    ///<summary>Ctor taking <paramref name="env"/>.</summary>
    public ApplicationStartup(IWebHostEnvironment env) {
      this.env= env;
      ApplicationStartup.log= ApplicationStartup.log ?? App.Logger<ApplicationStartup>();
      log.LogInformation("{appName} starting with ContentPath= '{contentPath}'\n\tWebRootPath= '{webRootPath}'", env.ApplicationName, env.ContentRootPath, env.WebRootPath);
    }

    ///<summary>Configure application service provider container.</summary>
    ///<remarks>This method gets called by the runtime before calling Configure().</remarks>
    public void ConfigureServices(IServiceCollection services) {
      services.ApplyConfigurators(App.Settings, APP_SVC_SECTION);
    }

    ///<summary>Configure the application middleware (HTTP request pipeline).</summary>
    ///<remarks>This method gets called by the runtime after services have been configured with ConfigureServices().</remarks>
    public void Configure(IApplicationBuilder app) {
      App.ServiceProv= app.ApplicationServices;
      App.AppLifetime.ApplicationStopped.Register(onShutdown);

      app.UseHostFiltering(); // Should be first in the pipeline
      if (isForwardedHeaders) app.UseForwardedHeaders();
      
      new MiddlewareContext() {
        HostingEnv= this.env,
        AppBuilder= app
      }.ApplyConfigurators(App.Settings, APP_MIDDLEWARE_SECTION);
    }
  }
}
