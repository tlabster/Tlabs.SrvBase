using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Tlabs.Config {

  ///<summary>Setup a process-exit shutdown handler</summary>
  ///<remarks>
  ///By default default <see cref="IHostApplicationLifetime"/> is only listening on Ctrl-C...
  ///(On windows closing a terminal window using the [x] is not recognized)
  ///<para>This also listens on <c>ProcessExit</c> which handles even windows close...</para>
  ////<remarks>
  public class ShutdownConfigurator : IConfigurator<MiddlewareContext> {
    static readonly ILogger log= Tlabs.App.Logger<ShutdownConfigurator>();

    ///<summary>Adds a general <c>ProcessExit</c> handler..</summary>
    public void AddTo(MiddlewareContext mware, IConfiguration cfg) {
      log.LogInformation("Register on-process-exit handler.");
      AppDomain.CurrentDomain.ProcessExit+= (src, e) => {
        log.LogInformation("Process exit...");
        App.AppLifetime.StopApplication();    //signal stopping
        if (App.AppLifetime is Microsoft.Extensions.Hosting.Internal.ApplicationLifetime ltm)
          ltm?.NotifyStopped();     //try to signal final stop
      };
    }
  }
}