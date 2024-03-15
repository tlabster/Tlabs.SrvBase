using System.Linq;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Tlabs.Config {

  ///<summary>Websocket configuration.</summary>
  public class WebsocketConfigurator : IConfigurator<MiddlewareContext> {
    readonly ILogger log= Tlabs.App.Logger<WebsocketConfigurator>();

    ///<inheritdoc/>
    public void AddTo(MiddlewareContext mware, IConfiguration cfg) {
      var wsCfg= cfg.GetSection("options");

      if (wsCfg.GetChildren().Any()) {
        var wsOpt= new WebSocketOptions();
        wsCfg.Bind(wsOpt);
        mware.AppBuilder.UseWebSockets(wsOpt);
      }
      else mware.AppBuilder.UseWebSockets();  //use default options
      log.LogInformation("Websocket middleware configured");
    }
  }

}