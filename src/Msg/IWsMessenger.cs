#nullable enable

using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Tlabs.Msg {

  /// <summary>Interface of a <see cref="WebSocket"/> messenger.</summary>
  public interface IWsMessenger<T> {
    /// <summary>Default scope.</summary>
    public const string DFLT_SCOPE= "_";

    /// <summary>Event fired on connection dropped.</summary>
    event Action<string>? DroppedScope;

    /// <summary>Register <paramref name="socket"/> connection with <paramref name="ctk"/> 
    /// and optional <paramref name="scope"/> and also optional <paramref name="msgReceiver"/>
    /// </summary>
    Task RegisterConnection(WebSocket socket, CancellationToken ctk, string scope= DFLT_SCOPE, Action<byte[], string>? msgReceiver= null);

    /// <summary>Publish <paramref name="message"/> to optional <paramref name="scope"/></summary>
    /// <remarks>Any messages published to a <paramref name="scope"/> that has no registered connections is ignored.
    /// </remarks>
    Task Publish(T message, string? scope= DFLT_SCOPE);

    /// <summary>Send <paramref name="message"/> to WS connection identified with <paramref name="sockTsk"/> returned from <see cref="RegisterConnection(WebSocket, CancellationToken, string, Action{byte[], string}?)"/></summary>
    /// <remarks>In contrast to <see cref="Publish(T, string?)"/> this sends *only* to the WS connewction identified with <paramref name="sockTsk"/>.
    /// </remarks>
    Task Send(T message, Task sockTsk);
  }
}