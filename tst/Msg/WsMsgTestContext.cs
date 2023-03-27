using System;
using System.Threading;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

using Tlabs.Misc;
using Tlabs.Config;

namespace Tlabs.Msg.Intern.Test {


  public class WsMsgTestContext : IDisposable {
    public WsMsgTestContext() {
      var svcProv = new SvcProvFactory().SvcProv;    //register TestAppLifetime service
    }
    public void Dispose() { }
  }

  class SvcProvFactory : AbstractServiceProviderFactory {
    public SvcProvFactory() {

      var logFac = LoggerFactory.Create(log => {
        log.AddConsole()
           .AddConsoleFormatter<CustomStdoutFormatter, CustomStdoutFormatterOptions>()
           .SetMinimumLevel(LogLevel.Information)
           .AddFilter("Microsoft", LogLevel.Warning);
      });
      App.LogFactory= logFac;

      // this.svcColl
      //     .AddLogging(log => log.AddFilter("", LogLevel.Information)
      //                           .AddFilter("System", LogLevel.Information)
      //                           .AddFilter("Microsoft", LogLevel.Information));

      this.svcColl.AddLogging();

      this.svcColl.AddSingleton<IHostApplicationLifetime, TestAppLifetime>();
      new Tlabs.Data.Serialize.Json.JsonFormat.Configurator().AddTo(svcColl, Tlabs.Config.Empty.Configuration);
    }

  }

  sealed class TestAppLifetime : IHostApplicationLifetime, IDisposable {
    static readonly CancellationToken cancelled = new(true);
    public readonly CancellationTokenSource CancellationTokSrc = new();

    public CancellationToken ApplicationStarted => cancelled;

    public CancellationToken ApplicationStopping => CancellationTokSrc.Token;

    public CancellationToken ApplicationStopped => CancellationTokSrc.Token;

    public void StopApplication() {
      CancellationTokSrc.Cancel();
    }

    public void Dispose() => CancellationTokSrc.Dispose();

  }

}