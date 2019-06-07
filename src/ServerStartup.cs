using System;
using System.IO;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Tlabs.Config;

namespace Tlabs.Server {
  using RTinfo= System.Runtime.InteropServices.RuntimeInformation;

  ///<summary>Server startup.</summary>
  public class ServerStartup {

    static Assembly entryAsm= Assembly.GetEntryAssembly();

    ///<summary>Create a <see cref="IWebHostBuilder"/>.</summary>
    public static IWebHostBuilder CreateServerHostBuilder(string[] args) {

      var hostSettings= App.Settings.GetSection("webHosting").ToConfigurationBuilder()
        .AddCommandLine(args)
        .Build();

      var logFactory= createLogFactory(hostSettings);
      return new WebHostBuilder()
        .UseConfiguration(hostSettings)
        .ConfigureServices(services => {
          services.AddSingleton<ILoggerFactory>(logFactory);
          services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        })
        .UseContentRoot(App.ContentRoot)
        .ApplyConfigurators(hostSettings, "configurator")
        .UseStartup<ServerStartup>();
    }

    private static ILoggerFactory createLogFactory(IConfigurationRoot config) {
      var logSettings= App.Settings.GetSection("logging");
      Environment.SetEnvironmentVariable("EXEPATH", Path.GetDirectoryName(App.MainEntryPath));
      var logFac= new LoggerFactory();
      logFac.AddConsole(logSettings)
            .AddFile(logSettings);
      App.LogFactory= logFac;
      Console.WriteLine(typeof(RTinfo).AssemblyQualifiedName);
      App.Logger<ServerStartup>().LogCritical(
        "*** {appName}\n" +
        "\t({path})\n" +
        "\ton {netVers} ({arch})\n" +
        "\t - {os}",
        entryAsm.FullName,
        entryAsm.Location,
        $"{RTinfo.FrameworkDescription} framwork {App.FrameworkVersion}", RTinfo.OSArchitecture,
        RTinfo.OSDescription);
      return logFac;
    }

    private static void onShutdown() {
      App.Logger<ServerStartup>().LogCritical("Shutdown.\n\n");
      Serilog.Log.CloseAndFlush();
    }

    private IHostingEnvironment env;
    private IServiceProvider svcProv;
    private ILogger log;

    ///<summary>Ctor taking <paramref name="env"/> and <paramref name="log"/> (from DI).</summary>
    public ServerStartup(IHostingEnvironment env, ILogger<ServerStartup> log) {
      this.env= env;
      this.log= log;
      this.log.LogInformation("{appName} starting...", env.ApplicationName);
      this.log.LogInformation("ContentRootPath: {path}", env.ContentRootPath);
      this.log.LogInformation("WebRootPath: {path}", env.WebRootPath);
    }

    ///<summary>Configure application service provider container.</summary>
    ///<remarks>This method gets called by the runtime before calling Configure().</remarks>
    public void ConfigureServices(IServiceCollection services) {
      services.ApplyConfigurators(App.Settings, "applicationServices");
    }

    ///<summary>Configure the application middleware (HTTP request pipeline).</summary>
    ///<remarks>This method gets called by the runtime after services have been configured with ConfigureServices().</remarks>
    public void Configure(IApplicationBuilder app) {
      App.ServiceProv= svcProv= app.ApplicationServices;
      App.AppLifetime.ApplicationStopped.Register(onShutdown);

      // var usrIdent= svcProv.GetRequiredService<IIdentityAccessor>();
      // log.LogInformation("Generic anonymous user: {usr}", usrIdent.Name);

      new MiddlewareContext() {
        HostingEnv= this.env,
        AppBuilder= app
      }.ApplyConfigurators(App.Settings, "applicationMiddleware");
    }
  }
}
