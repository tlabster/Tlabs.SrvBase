#nullable enable

using System;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

using Tlabs.Config;
using System.Buffers;

namespace Tlabs.Msg.Intern {

  /// <summary>Non generic base messenger.</summary>
  public class WsMessenger {
    /// <summary>Log</summary>
    protected static readonly ILogger log= Tlabs.App.Logger<WsMessenger>();
    /// <summary>Shared event</summary>
    protected static event Action<string>? sharedDroppedScope;
    /// <summary>Raise shared event</summary>
    protected static void onDroppedScope(string scope) => sharedDroppedScope?.Invoke(scope);


    /// <summary>Options</summary>
    public class Options {
      /// <summary>Message send timeout (ms)</summary>
      public int SendTimeout= 1_500;
      /// <summary>Max. size received message (bytes)</summary>
      public int MaxMsgSize= 10_000_000;
    }

    /// <summary>Default Options</summary>
    public class DefaulttOptions : IOptions<Options> {
      ///<inheritdoc/>
      public Options Value => new();
    }
    /// <summary>Class representing a <see cref="WebSocket"/>.</summary>
    protected class SocketConnection : IDisposable {
      static readonly ILogger log= Tlabs.App.Logger<SocketConnection>();

      readonly TaskCompletionSource tcs= new();
      readonly WebSocket ws;
      readonly CancellationToken ctk;
      readonly Action<SocketConnection>? onDispose;
      readonly Options opt;
      uint isDisposed;

      /// <summary>Ctor.</summary>
      public SocketConnection(
        WebSocket ws,
        string scope,
        Options opt,
        CancellationToken ctk,
        Action<ReadOnlyMemory<byte>, string>? receivePayloadMsg= null,
        Action<SocketConnection>? onDispose= null
      ) {
        this.ws= ws;
        this.Scope= scope;
        this.opt= opt;
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

      private void startReceivingMessages(Action<ReadOnlyMemory<byte>, string>? msgReciever) {
        int msgChnk= 2048;
        var buffer= new ArrayBufferWriter<byte>(msgChnk);
        /* Start the message reception pump in background:
         * NOTE: The socket MUST always listen to receive messages in order to have the close handshake being processed...
         */
        _= Task.Run(async ()=> {
          try {
            while (IsReady && !ctk.IsCancellationRequested) {
              var res= await ws.ReceiveAsync(buffer.GetMemory(buffer.FreeCapacity), ctk);
              buffer.Advance(res.Count);
              // if (res.MessageType == WebSocketMessageType.Close) Dispose();
              if (res.MessageType != WebSocketMessageType.Text) throw new WebSocketException("Invalid message type");

              /* Recieve one complete message:
              */
              while (!res.EndOfMessage) {
                if (!IsReady || ctk.IsCancellationRequested) return;
                if (buffer.WrittenCount >= opt.MaxMsgSize) {
                  buffer.ResetWrittenCount();
                  while (!res.EndOfMessage) res= await ws.ReceiveAsync(buffer.GetMemory(buffer.FreeCapacity), ctk);
                  log.LogWarning("Msg. exeeded max. size ({sz}) - skipped!", opt.MaxMsgSize);
                  break;
                }
                msgChnk= Math.Min(2*msgChnk, opt.MaxMsgSize - buffer.WrittenCount);
                res= await ws.ReceiveAsync(buffer.GetMemory(msgChnk), ctk);
                buffer.Advance(res.Count);
              }
              if (buffer.WrittenCount > 0) try {
                msgReciever?.Invoke(buffer.WrittenMemory, Scope);     //call message reciever
              }
              catch (Exception e) { log.LogWarning("Error receiving message ({msg})", e.Message); }
              buffer.ResetWrittenCount();
            }
          }
          catch (Exception e) when (e is not TaskCanceledException) { log.LogError(e, "WebSocket error"); }
          finally {
            log.LogDebug("Connection closed by client.");
            Dispose();
          }
        }, ctk);
      }

      ///<inheritdoc/>
      public void Dispose() {
        if (0 != Interlocked.Exchange(ref isDisposed, 1)) return;
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
        svc.Configure<WsMessenger.Options>(cfg.GetSection("options"));
        svc.AddSingleton(typeof(IWsMessenger<>), typeof(WsMessenger<>));
        log.LogInformation("{m} service configured.", nameof(WsMessenger));
      }
    }
  }

}