using System.IO;
using System.Linq;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Tlabs.Server.Tests {

  [Collection("App.ServiceProv")]   //All tests of classes with this same collection name do never run in parallel /https://xunit.net/docs/running-tests-in-parallel)
  public class ServerSetupTest {

    [Fact]
    public void SetupTest() {
      var appFile= Path.Combine(App.Setup.ContentRoot, "appsettings.json");
      File.Delete(appFile);
      File.WriteAllText(appFile, $"{{ \"{ApplicationStartup.DFLT_HOST_SECTION}\": {{ }} }}");
      var dfltSvcProv= App.ServiceProv;
      var dfltAppLft= App.AppLifetime;

      var hostBuilder= ApplicationStartup.CreateServerHostBuilder();
      Assert.NotNull(hostBuilder);

      using var host= hostBuilder.Build();
      Assert.NotNull(host);
      Assert.NotEmpty(App.Settings.GetChildren());
      Assert.Equal(ApplicationStartup.DFLT_HOST_SECTION, App.Settings.GetChildren().Single().Key);

      Assert.NotEqual(dfltSvcProv, App.ServiceProv);
      Assert.NotEqual(dfltAppLft, App.AppLifetime);
      Assert.NotNull(App.ServiceProv.GetService(typeof(IServiceScopeFactory)));

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

  }
}
