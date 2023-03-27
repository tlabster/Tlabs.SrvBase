#nullable enable

using System;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

using Tlabs.Config;

namespace Tlabs.Msg.Intern {

  /// <summary>Non generic base messenger.</summary>
  public class WsMessenger {
    /// <summary>Log</summary>
    protected static readonly ILogger log= Tlabs.App.Logger<WsMessenger>();
    /// <summary>Shared event</summary>
    protected static event Action<string>? sharedDroppedScope;
    /// <summary>Raise shared event</summary>
    protected static void onDroppedScope(string scope) => sharedDroppedScope?.Invoke(scope);


    /// <summary>Class representing a <see cref="WebSocket"/>.</summary>
    protected class SocketConnection : IDisposable {
      const int MAX_MSG_SZ= 4096;
      static readonly ILogger log= Tlabs.App.Logger<SocketConnection>();

      readonly TaskCompletionSource tcs= new();
      readonly WebSocket ws;
      readonly CancellationToken ctk;
      readonly Action<SocketConnection>? onDispose;
      bool isDisposed;

      /// <summary>Ctor.</summary>
      public SocketConnection(
        WebSocket ws,
        string scope,
        CancellationToken ctk,
        Action<byte[], string>? receivePayloadMsg= null,
        Action<SocketConnection>? onDispose= null) {
        this.ws= ws;
        this.Scope= scope;
        this.ctk= ctk;
        this.onDispose= onDispose;
        ctk.Register(Dispose);
        this.startReceivingMessages(receivePayloadMsg);
      }

      /// <summary>Sync semaphore to be acquired with Wait() before any await WriteMessage().</summary>
      public SemaphoreSlim SendSync { get; }= new(1);

      /// <summary>Scope</summary>
      public string Scope { get; }

      /// <summary>Task</summary>
      public Task Task => tcs.Task;

      /// <summary>True if socket is open</summary>
      public bool IsReady => ws.State == WebSocketState.Open;

      /// <summary>Write <paramref name="msgData"/></summary>
      public Task WriteMessage(byte[] msgData) => ws.SendAsync(msgData, WebSocketMessageType.Text, true, ctk);

      private void startReceivingMessages(Action<byte[], string>? msgReciever) {
        var buffer= new byte[MAX_MSG_SZ];
        var bufSeg= new ArraySegment<byte>(buffer);
        /* Start the message reception pump in background:
         * NOTE: The socket MUST always listen to receive messages in order to have the close handshake being processed...
         */
        _= ws.ReceiveAsync(bufSeg, ctk)
             .ContinueWith(async tsk => {
               try {
                 var res= await tsk;    //throws on receiving error
                 while (true) {
                   /* Recieve one complete message:
                   */
                   while (IsReady && !ctk.IsCancellationRequested) try {
                       // if (res.MessageType == WebSocketMessageType.Close) Dispose();
                       if (res.MessageType != WebSocketMessageType.Text) throw new WebSocketException("Invalid message type");
                       if (res.EndOfMessage) {
                         if (res.Count > 0) msgReciever?.Invoke(buffer[..res.Count], Scope);     //call message reciever
                         break;
                       }
                       if (res.Count >= bufSeg.Count) {        //max. message size exeeded
                         log.LogWarning("Msg. exeeded max. size ({sz}) - skipped!", MAX_MSG_SZ);
                         while (IsReady && !(await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ctk)).EndOfMessage) ;    //read to end
                         continue;   //with next message
                       }
                       bufSeg= new ArraySegment<byte>(buffer, res.Count, buffer.Length - res.Count);
                       res= await ws.ReceiveAsync(bufSeg, ctk);
                     }
                     catch (Exception e) {
                       log.LogWarning("Error receiving message ({msg})", e.Message);
                       break;
                     }
                   if (!IsReady || ctk.IsCancellationRequested) break;
                   bufSeg= new ArraySegment<byte>(buffer);
                   res= await ws.ReceiveAsync(bufSeg, ctk);  //start receiving next message
                 }
               }
               finally {
                 log.LogDebug("Connection closed by client.");
                 Dispose();
               }
             }, ctk);
      }

      ///<inheritdoc/>
      public void Dispose() {
        if (isDisposed) return;     //already disposed
        isDisposed= true;
        try {
          onDispose?.Invoke(this);
          ws.Abort();
        }
        catch (Exception e) { log.LogDebug($"Problem on {nameof(SocketConnection)} cancellation: {{msg}}", e.Message); }
        tcs.TrySetResult(); //mark socket task as completed
      }
    }

    /// <summary>Configurator</summary>
    public class Configurator : IConfigurator<IServiceCollection> {
      ///<inheritdoc/>
      public void AddTo(IServiceCollection svc, IConfiguration cfg) {
        svc.AddSingleton(typeof(IWsMessenger<>), typeof(WsMessenger<>));
        log.LogInformation("{m} service configured.", nameof(WsMessenger));
      }
    }
  }

}