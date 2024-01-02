using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Tlabs.Config {

  ///<summary>Websocket configuration.</summary>
  public class WebsocketConfigurator : IConfigurator<MiddlewareContext> {
    readonly ILogger log= Tlabs.App.Logger<WebsocketConfigurator>();

    ///<inheritdoc/>
    public void AddTo(MiddlewareContext mware, IConfiguration cfg) {
      mware.AppBuilder.UseWebSockets();
      log.LogInformation("Websocket middleware configured");
    }
  }

}