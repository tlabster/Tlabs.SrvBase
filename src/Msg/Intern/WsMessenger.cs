using System;
using System.Buffers;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Tlabs.Misc;
using Tlabs.Sync;
using Tlabs.Data.Serialize;

namespace Tlabs.Msg.Intern {

  /// <summary><see cref="WebSocket"/> messenger.</summary>
  ///<remarks>Publishes message(s) to all <see cref="WebSocket"/> connections registered with same scope.
  ///(see: <see cref="RegisterConnection(WebSocket, CancellationToken, string, Action{byte[], string}?)"/>)
  ///</remarks>
  public sealed class WsMessenger<T> : WsMessenger, IWsMessenger<T>, IDisposable {
    readonly ISerializer<T> json;
    readonly Options opt;
    readonly SyncCollection<SocketConnection> connections= Singleton<SyncCollection<SocketConnection>>.Instance;

    /// <inheritdoc/>
    public event Action<string>? DroppedScope {
      add => sharedDroppedScope+= value;
      remove => sharedDroppedScope-= value;
    }

    /// <summary>Ctor from <paramref name="json"/> serializer.</summary>
    public WsMessenger(ISerializer<T> json) : this(json, new DefaulttOptions()) { }

    /// <summary>Ctor from <paramref name="json"/> serializer and <paramref name="options"/>.</summary>
    public WsMessenger(ISerializer<T> json, IOptions<Options> options) {
      this.json= json;
      this.opt= options.Value;
      startConnectionStateWatcher(App.AppLifetime.ApplicationStopping);
    }

    /// <inheritdoc/>
    [Obsolete("Use overload taking Action<ReadOnlyMemory<byte>, string>? msgReceiver", error: false)]
    public Task RegisterConnection(WebSocket socket, CancellationToken ctk, string? scope, Action<byte[], string>? receiveMessageData) {
      void msgReceiver(ReadOnlySequence<byte> msg, string scope) => receiveMessageData?.Invoke(msg.ToArray(), scope);
      if (ctk.IsCancellationRequested) return Task.FromCanceled(ctk);
      var con= new SocketConnection(socket, scope ?? IWsMessenger<T>.DFLT_SCOPE, opt, ctk, msgReceiver, handleSocketConnectionDispose);
      connections.Add(con);
      return con.Task;
    }

    /// <inheritdoc/>
    public Task RegisterConnection(WebSocket socket, CancellationToken ctk, string? scope= null, Action<ReadOnlySequence<byte>, string>? receiveMessageData= null) {
      if (ctk.IsCancellationRequested) return Task.FromCanceled(ctk);
      var con= new SocketConnection(socket, scope ?? IWsMessenger<T>.DFLT_SCOPE, opt, ctk, receiveMessageData, handleSocketConnectionDispose);
      connections.Add(con);
      return con.Task;
    }

    /// <inheritdoc/>
    public Task Publish(T message, string? scope= IWsMessenger<T>.DFLT_SCOPE) {
      var scopedConnections= connections.CollectionOf(c => c.Scope == scope);
      if (0 == scopedConnections.Count) {
        log.LogDebug("Can't publish to scope {scope}, no web-socket connection.", scope);
        onDroppedScope(scope??IWsMessenger<T>.DFLT_SCOPE);    //signal dropped scope
        return Task.CompletedTask;      //no websocket connection(s) in scope
      }
      return sendToConnections(scopedConnections, message);
    }

    /// <inheritdoc/>
    public Task Send(T message, Task sockTsk) {
      var tskConns= connections.CollectionOf(c => c.Task == sockTsk);
      if (0 == tskConns.Count) return Task.CompletedTask;      //no websocket found
      return sendToConnections(tskConns, message);
    }

    private async Task sendToConnections(ICollection<SocketConnection> conns, T payload) {
      var msg= json.WriteObj(payload);

      foreach (var con in conns) try {    //***TODO: consider to run this in parallel
        if (!con.IsReady) { //detect closed socket
          con.Dispose();
          continue;
        }
        try {
          /* No concurrent send operation (including await...) allowed per connection:
            */
          if (!await con.SendSync.WaitAsync(opt.SendTimeout)) throw new TimeoutException();
          await con.WriteMessage(msg).Timeout(opt.SendTimeout);   //we do not want to await the msg write forever
        }
        finally {
          con.SendSync.Release();
        }
      }
      catch (Exception e) {
        con.Dispose();    //dispose bad connetion and close socket
        log.LogInformation("Websocket failure ({msg})", e.Message);
      }
    }

    ///<inheritdoc/>
    public void Dispose() {
      foreach (var con in connections) con.Dispose(); //Dispose off all websocket connections
    }

    void handleSocketConnectionDispose(SocketConnection disposedCon) {
      /* This is called on SocketConnection.Dispose() or external token cancellation.
       * Here we remove the connection from the connections list and fire the DroppedScope event...
       */
      log.LogDebug("Dropping socket connection for scope: {scope}", disposedCon.Scope);
      try {
        if (   connections.Remove(disposedCon)
            && !connections.Contains(con => con.Scope == disposedCon.Scope))
          onDroppedScope(disposedCon.Scope); //no more connection for Scope
      }
      catch (Exception e) {
        log.LogInformation("Failed to drop scope: {scope} ({msg})", disposedCon.Scope, e.Message);
        log.LogDebug(e, "Drop scope error details:");
      }
    }

    void startConnectionStateWatcher(CancellationToken ctk) {
      /* Run a background task that periodically checks
       * if there are any stale websocket connection to be removed from the
       * list of registered connections to publish messages to....
       */
      _= Task.Run(async () => {
        while (!ctk.IsCancellationRequested) {
          await Task.Delay(15_000, ctk);
          if (ctk.IsCancellationRequested) return;
          foreach (var badCon in connections.CollectionOf(con => !con.IsReady)) {
            log.LogWarning("Disposing stale web-socket connection: {con}...", badCon.Scope);
            badCon.Dispose();             //dispose stale connection
            connections.Remove(badCon);
          }
        }
        log.LogDebug("Connection state watcher canceled.");
      }, ctk);
    }

  }

}
