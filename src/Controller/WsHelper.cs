using System;
using System.Threading;
using System.Threading.Tasks;
using System.Net.WebSockets;

using Microsoft.AspNetCore.Http;

namespace Tlabs.Server.Controller {

  /// <summary><see cref="WebSocket"/> helper</summary>
  /// <example>
  /// This shows a typicall controller action method to handle a <see cref="WebSocket"/> connection: 
  /// <code>
  /// [HttpGet("{scope}/{index?}")]
  /// public async Task OpenWebSocket(string scope, string? parameter) {
  ///   await WsHelper.WithSocketConnection(HttpContext, (webSocket, ctk) => {
  ///     var sockConn= wsMsg.RegisterConnection(socket, ctk, scope, module.AckMsgReciever);
  ///     await module.PublishModuleData(ctk);
  ///     await sockConn;
  ///   });
  /// }
  /// </code>
  /// </example>
  public static class WsHelper {

    /// <summary><paramref name="run"/> delegate with <paramref name="httpCtx"/></summary>
    public static async Task WithSocketConnection(HttpContext httpCtx, Func<WebSocket, CancellationToken, Task> run) {

      if (!httpCtx.WebSockets.IsWebSocketRequest) {
        httpCtx.Response.StatusCode= StatusCodes.Status400BadRequest;
        return;
      }

      using var webSocket= await httpCtx.WebSockets.AcceptWebSocketAsync();
      using var cts= CancellationTokenSource.CreateLinkedTokenSource(httpCtx.RequestAborted, Tlabs.App.AppLifetime.ApplicationStopping);
      var ctk= cts.Token;

      try {
        await run(webSocket, ctk);
      }
      finally {
        cts.Cancel();
      }

    }
  }
}