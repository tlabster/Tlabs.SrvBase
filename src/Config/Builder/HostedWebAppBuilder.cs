using System;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HostFiltering;

namespace Tlabs.Config {

  /// <summary>Interface of an (hosted) <see cref="WebApplication"/> builder.</summary>
  public interface IHostedWebAppBuilder : IHostApplicationBuilder {
    /// <summary><see cref="IHostBuilder"/> implementation for programatic configuration.</summary>
    public ConfigureHostBuilder Host { get; }
    /// <summary><see cref="IWebHostBuilder"/> implementation for programatic configuration.</summary>
    public ConfigureWebHostBuilder WebHost { get; }
    /// <summary>Builds the <see cref="WebApplication"/>.</summary>
    public WebApplication Build();
  }


  /// <summary>Hosted <see cref="WebApplication"/> builder.</summary>
  /// <remarks>
  /// The typical main entry poitn of a web application could look like this:
  /// <code>
  ///public static async Task Main(string[] args) {
  ///
  ///   var webAppBuilder= new HostedWebAppBuilder(args);
  ///
  ///   /* As an advanced option one could apply additional custom configuration
  ///    * that has not already been applied through the appsettings configuration
  ///    * -> right here &lt;-
  ///    */
  ///
  ///   var webApp= webAppBuilder.Build();
  ///
  ///   /*
  ///    * Additional API end-points could be programatically declared like:
  ///    * webApp.MapGet("/hello", () => "Hello, world!");
  ///    */
  ///
  ///   await webApp.RunAsync();
  ///}
  /// </code>
  /// </remarks>
  public sealed class HostedWebAppBuilder : BaseHostedAppBuilder, IHostedWebAppBuilder {
    class WebAppBuilderFactory : IHostedBuilderFactory {
      public IHostApplicationBuilder Create(IConfigurationSection hostConfig, string[]? args) {
        var builder= WebApplication.CreateEmptyBuilder(new() {
          ContentRootPath= App.ContentRoot,
          ApplicationName= App.Setup.Name,
          EnvironmentName= App.Setup.EnvironmentName,
          Args= args,
          WebRootPath= hostConfig[WebHostDefaults.WebRootKey]
        });
        builder.Configuration.AddConfiguration(hostConfig);
        return builder;
      }
    }

    internal const string DFLT_WEBHOST_SECTION= "webHosting";
    internal const string APP_MIDDLEWARE_SECTION= "applicationMiddleware";
    internal static readonly string[] ANY_HOST= new[] { "*" };

    string middlewareSectionName;
    WebApplicationBuilder webAppBuilder;
    WebHostBuilderContext builderCtx;

    /// <summary>Ctor from optional <paramref name="args"/>.</summary>
    public HostedWebAppBuilder(string[]? args= null) : this(DFLT_WEBHOST_SECTION, APP_MIDDLEWARE_SECTION, args) { }

    /// <summary>Ctor from optional <paramref name="hostSectionName"/>, <paramref name="middlewareSectionName"/> and optional <paramref name="args"/>.</summary>
    public  HostedWebAppBuilder(string hostSectionName, string middlewareSectionName, string[]? args= null)
      : base(new WebAppBuilderFactory(), hostSectionName, args) {
      this.middlewareSectionName= middlewareSectionName;
      this.webAppBuilder= (WebApplicationBuilder)this.hostedAppBuilder;

#pragma warning disable CA1859
      var webHostBuilder= (IWebHostBuilder)webAppBuilder.WebHost;
      webHostBuilder.ConfigureAppConfiguration((ctx, b) => this.builderCtx= ctx);
#pragma warning restore CA1859
      if (null == this.builderCtx) throw new InvalidOperationException($"Missing {nameof(WebHostBuilderContext)}");

      webHostBuilder.ApplyConfigurators(hostConfig, "configurator");      //<- this is where typically Kestrel would be configured
      webAppBuilder.Host.UseConsoleLifetime();
      configureDefaultHostFiltering(webAppBuilder, hostConfig);
    }

    static void configureDefaultHostFiltering(IHostApplicationBuilder appBuilder, IConfiguration hostConfig) {
      // Fallback
      appBuilder.Services.PostConfigure<HostFilteringOptions>(options => {
        if (options.AllowedHosts == null || options.AllowedHosts.Count == 0) {
          // "AllowedHosts": "localhost;127.0.0.1;[::1]"
          var hosts= hostConfig["AllowedHosts"]?.Split(';' , StringSplitOptions.RemoveEmptyEntries);
          // Fall back to "*" to disable.
          options.AllowedHosts= (hosts?.Length > 0 ? hosts : ANY_HOST);
        }
      });
      // Change notification
      appBuilder.Services.AddSingleton<IOptionsChangeTokenSource<HostFilteringOptions>>(
        new ConfigurationChangeTokenSource<HostFilteringOptions>(hostConfig)
      );
      /*
       * This should be added by the middelware configuration:
      services.AddTransient<IStartupFilter, HostFilteringStartupFilter>();                //app.UseHostFiltering()
      services.AddTransient<IStartupFilter, ForwardedHeadersStartupFilter>();             //app.UseForwardedHeaders()
      services.AddTransient<IConfigureOptions<ForwardedHeadersOptions>, ForwardedHeadersOptionsSetup>();
      */
    }


    ///<inheritdoc/>
    public ConfigureHostBuilder Host => webAppBuilder.Host;
    ///<inheritdoc/>
    public ConfigureWebHostBuilder WebHost => webAppBuilder.WebHost;

    ///<inheritdoc/>
    public WebApplication Build() {
      var webApp= webAppBuilder.Build();
      this.setupHost(webApp);

      webApp.UseHostFiltering(); // Should be first in the pipeline
      new MiddlewareContext() {
        HostingEnv= builderCtx.HostingEnvironment,
        AppBuilder= webApp
      }.ApplyConfigurators(App.Settings, middlewareSectionName);

      return webApp;
    }

  }
}
