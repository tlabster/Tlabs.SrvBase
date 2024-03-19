using System.IO;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;

using Tlabs.Config;

using Xunit;
#pragma warning disable CS0618
namespace Tlabs.Server.Tests {

  [Collection("App.ServiceProv")]   //All tests of classes with this same collection name do never run in parallel /https://xunit.net/docs/running-tests-in-parallel)
  public class ServerSetupTest {
    static int cfgCnt;

    [Fact]
    public void SetupTest() {
      var appFile= Path.Combine(App.Setup.ContentRoot, "appsettings.json");
      File.Delete(appFile);
      File.WriteAllText(appFile, APPSETTINGS);
      var dfltSvcProv= App.ServiceProv;
      var dfltAppLft= App.AppLifetime;
      cfgCnt= 0;

      var hostBuilder= ApplicationStartup.CreateServerHostBuilder();
      Assert.NotNull(hostBuilder);

      using var host= hostBuilder.Build();
      Assert.NotNull(host);

      Assert.NotEqual(dfltSvcProv, App.ServiceProv);
      Assert.NotEqual(dfltAppLft, App.AppLifetime);
      Assert.Same(App.ServiceProv, host.Services);
      Assert.Same(App.LogFactory, host.Services.GetRequiredService<ILoggerFactory>());

      Assert.Equal(1, cfgCnt);
      Assert.NotNull(App.ServiceProv.GetService<ServerSetupTest>());
      // Assert.NotNull(App.ServiceProv.GetService<IHostedService>());
      var webHostEnv= App.ServiceProv.GetService<IWebHostEnvironment>();
      Assert.NotNull(webHostEnv);
      Assert.EndsWith("rsc/test", webHostEnv.WebRootPath.Replace("\\", "/"));

#if START_TEST
      var stopCnt=0;
      App.AppLifetime.ApplicationStopped.Register(()=> ++stopCnt);

      await host.StartAsync();
      host.Run();
      App.AppLifetime.StopApplication();    //this seems to cancel all worker tasks - and breaks other async tests
      await host.StopAsync();
      (App.ServiceProv as IDisposable)?.Dispose();
      Assert.Equal(1, stopCnt);
#endif
    }

    [Fact]
    public void HostedWebAppBuilderTest() {
      var appFile= Path.Combine(App.Setup.ContentRoot, "appsettings.json");
      File.Delete(appFile);
      File.WriteAllText(appFile, APPSETTINGS);
      var dfltSvcProv= App.ServiceProv;
      var dfltAppLft= App.AppLifetime;
      cfgCnt= 0;

      var webAppBuilder= new HostedWebAppBuilder();
      Assert.NotNull(webAppBuilder);

      webAppBuilder.WebHost.UseKestrel();

      using var host= webAppBuilder.Build();
      Assert.NotNull(host);

      Assert.NotEqual(dfltSvcProv, App.ServiceProv);
      Assert.NotEqual(dfltAppLft, App.AppLifetime);
      Assert.Same(App.ServiceProv, host.Services);
      Assert.Same(App.LogFactory, host.Services.GetRequiredService<ILoggerFactory>());

      Assert.Equal(1, cfgCnt);
      Assert.NotNull(App.ServiceProv.GetService<ServerSetupTest>());
      // Assert.NotNull(App.ServiceProv.GetService<IHostedService>());
      var webHostEnv= App.ServiceProv.GetService<IWebHostEnvironment>();
      Assert.NotNull(webHostEnv);
      Assert.EndsWith("rsc/test", webHostEnv.WebRootPath.Replace("\\", "/"));

#if START_TEST
      var stopCnt=0;
      App.AppLifetime.ApplicationStopped.Register(()=> ++stopCnt);

      await host.StartAsync();
      host.Run();
      App.AppLifetime.StopApplication();    //this seems to cancel all worker tasks - and breaks other async tests
      await host.StopAsync();
      (App.ServiceProv as IDisposable)?.Dispose();
      Assert.Equal(1, stopCnt);
#endif
    }


    const string APPSETTINGS= $"{{ \"{ApplicationStartup.DFLT_HOST_SECTION}\": " + @"{
  ""urls"": ""http://+:8001"",
  ""webroot"": ""rsc/test"",
  ""configurator"": {
    ""hostCfg"": {
      ""ord"": 3,
      ""type"": ""Tlabs.Server.Tests.ServerSetupTest+TestConfigurator, Tlabs.SrvBase.Tests""
    }
  }
}}";

    public class TestConfigurator : IConfigurator<IWebHostBuilder> {
      public void AddTo(IWebHostBuilder builder, IConfiguration cfg) {
        ++cfgCnt;
        builder.ConfigureServices(svc => svc.AddSingleton<ServerSetupTest>());
      }
    }
  }
}
