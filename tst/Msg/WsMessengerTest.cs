using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using System.Net.WebSockets;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

using Tlabs.Misc;
using Tlabs.Config;
using Tlabs.Data.Serialize;

using Xunit;
using Xunit.Abstractions;
using Moq;

namespace Tlabs.Msg.Intern.Test {

  [Collection("App.ServiceProv")]   //All tests of classes with this same collection name do never run in parallel /https://xunit.net/docs/running-tests-in-parallel)
  public class WsMessengerTest : IClassFixture<WsMessengerTest.Fixture> {
    static int fixtureSetupCnt= 0;
    public class Fixture : AbstractServiceProviderFactory {
      public Fixture() {
        if (++fixtureSetupCnt > 1) throw new InvalidOperationException($"fixtureSetupCnt: {fixtureSetupCnt}");

        this.svcColl.AddSingleton<IHostApplicationLifetime, TestAppLifetime>();
        new Tlabs.Data.Serialize.Json.JsonFormat.Configurator().AddTo(svcColl, Tlabs.Config.Empty.Configuration);

        Assert.NotNull(this.SvcProv);   //make sure the IServiceProvider gets initialized...
      }

      sealed class TestAppLifetime : IHostApplicationLifetime, IDisposable {
        static readonly CancellationToken cancelled= new(true);
        public readonly CancellationTokenSource CancellationTokSrc= new();

        public CancellationToken ApplicationStarted => cancelled;

        public CancellationToken ApplicationStopping => CancellationTokSrc.Token;

        public CancellationToken ApplicationStopped => CancellationTokSrc.Token;

        public void StopApplication() {
          CancellationTokSrc.Cancel();
        }

        public void Dispose() => CancellationTokSrc.Dispose();

      }
    }

    Fixture tstCtx;
    ITestOutputHelper tstout;

    public WsMessengerTest(Fixture tstCtx, ITestOutputHelper tstout) {
      this.tstCtx= tstCtx;
      this.tstout= tstout;

      // tstout.WriteLine(DbgHelper.ProcInfo());
      // Tlabs.Config.DbgHelper.HardBreak();
    }

    [Fact]
    public async void BasicPublishTest() {
      tstout.WriteLine("BasicPublishTest running...");
      int scopeDropCnt= 0;
      var wsMsg= new WsMessenger<TestMsg>(App.ServiceProv.GetRequiredService<ISerializer<TestMsg>>());
      var wsMsg2= new WsMessenger<TestMsg2>(App.ServiceProv.GetRequiredService<ISerializer<TestMsg2>>());
      wsMsg.DroppedScope+= scope => ++scopeDropCnt;

      var reqTokenSrc= new CancellationTokenSource();
      var wrSock= new WriteOnlyMockSocket();
      var tstMsg= new TestMsg();
      var tstMsg2= new TestMsg2();
      using (var ws= wrSock.Socket) {
        var conTsk= wsMsg.RegisterConnection(ws, reqTokenSrc.Token);
        Assert.False(wrSock.IsAborted);
        Assert.False(wrSock.IsDisposed);
        Assert.False(conTsk.IsCompleted);

        await wsMsg.Publish(tstMsg);
        await wsMsg.Publish(tstMsg, "ignore");    //tstMsg for this scope must not be written to the socket
        Assert.Equal(1, scopeDropCnt);            //drop scope "ignore"
        await wsMsg2.Publish(tstMsg2);
        Assert.Equal(2, wrSock.SendCnt);          //tstMsg & tstMsg2 published to same socket

        reqTokenSrc.Cancel();                     //Simulate a canceled token from HttpContext.RequestAborted
        Assert.Equal(2, scopeDropCnt);            //drop scope "_"
        await wsMsg.Publish(tstMsg);
        Assert.Equal(3, scopeDropCnt);            //drop scope "_" again
        Assert.Equal(2, wrSock.SendCnt);  //no more msg published
        Assert.True(wrSock.IsAborted);
        Assert.False(wrSock.IsDisposed);
        Assert.True(conTsk.IsCompleted || conTsk.IsCanceled);
      }
      Assert.True(wrSock.IsDisposed);
      wsMsg.Dispose();
      wsMsg2.Dispose();
    }

    [Fact]
    public async void PrematureSocketCloseTest() {
      tstout.WriteLine("PrematureSocketCloseTest running...");
      var wsMsg= new WsMessenger<TestMsg>(App.ServiceProv.GetRequiredService<ISerializer<TestMsg>>());

      var reqTokenSrc= new CancellationTokenSource();
      var wrSock= new WriteOnlyMockSocket();
      var tstMsg= new TestMsg();
      using (var ws= wrSock.Socket) {
        var conTsk= wsMsg.RegisterConnection(ws, reqTokenSrc.Token);

        wrSock.SocketSate= WebSocketState.Closed;     //Simulate a premature socket close
        await wsMsg.Publish(tstMsg);
        Assert.Equal(0, wrSock.SendCnt);
        Assert.True(wrSock.IsAborted);
        Assert.False(wrSock.IsDisposed);
        Assert.True(conTsk.IsCompleted || conTsk.IsCanceled);
      }
      Assert.True(wrSock.IsDisposed);

      wsMsg.Dispose();
    }

    [Fact]
    public async void ReceiveMessageTest() {
      tstout.WriteLine("ReceiveMessageTest running...");
      var wsMsg= new WsMessenger<TestMsg>(App.ServiceProv.GetRequiredService<ISerializer<TestMsg>>());

      var reqTokenSrc= new CancellationTokenSource();
      var rwSock= new ReadWriteMockSocket();
      var msgReceived= new TaskCompletionSource();
      // var tstMsg= new TestMsg();
      ReadOnlySequence<byte> recMem= default;
      string recScope= null;
      using (var ws= rwSock.Socket) {
        var conTsk= wsMsg.RegisterConnection(ws, reqTokenSrc.Token, "_", (ReadOnlySequence<byte> buf, string scope) => {
          recMem= buf;
          recScope= scope;
          rwSock.ListeningRdySrc= new();
          msgReceived.TrySetResult();
        });
        await rwSock.ListeningRdySrc.Task.Timeout(200);
        Assert.Equal(1, rwSock.waitToReceiveCnt);
        Assert.True(recMem.IsEmpty);
        await Task.Yield();

        rwSock.Receive("{  }");
        await rwSock.ReceivedSrc.Task.Timeout(200);
        await msgReceived.Task;//.Timeout(200);
        Assert.Equal("_", recScope);
        await rwSock.ListeningRdySrc.Task.Timeout(200);
        Assert.Equal(2, rwSock.waitToReceiveCnt);

        rwSock.waitToReceiveCnt= 0;
        msgReceived= new();
        rwSock.Receive($"{{ {new string(' ', 4096 + 1234)} }}");   //msg > 4096 bytes
        await msgReceived.Task;//.Timeout(200);
        await rwSock.ListeningRdySrc.Task;//.Timeout(200);
        Assert.Equal(2, rwSock.waitToReceiveCnt);

        rwSock.waitToReceiveCnt= 0;
        recMem= default;
        recScope= null;
        reqTokenSrc.Cancel();             //Simulate a canceled token from HttpContext.RequestAborted

        rwSock.Receive("{ x }");
        await rwSock.ListeningRdySrc.Task.Timeout(200);
        Assert.Null(recScope);
        Assert.Equal(0, rwSock.waitToReceiveCnt);   //no more received

        Assert.Equal(0, rwSock.SendCnt);
        Assert.True(rwSock.IsAborted);
        Assert.True(conTsk.IsCompleted || conTsk.IsCanceled);
      }
      Assert.True(rwSock.IsDisposed);

      wsMsg.Dispose();
    }

    [Fact]
    public async void MultiSocketPublishTest() {
      tstout.WriteLine("MultiSocketPublishTest running...");
      int scopeDropCnt= 0;
      var wsMsg= new WsMessenger<TestMsg>(App.ServiceProv.GetRequiredService<ISerializer<TestMsg>>());
      wsMsg.DroppedScope+= scope => {
        ++scopeDropCnt;
      };

      var reqTokenSrc= new CancellationTokenSource();
      var wrSock= new WriteOnlyMockSocket();
      var tstMsg= new TestMsg();
      using (var ws1= wrSock.Socket) {
        var conTsk= wsMsg.RegisterConnection(ws1, reqTokenSrc.Token);
        Assert.False(wrSock.IsAborted);
        Assert.False(wrSock.IsDisposed);
        Assert.False(conTsk.IsCompleted);

        var reqTokenSrc2= new CancellationTokenSource();
        var wrSock2= new WriteOnlyMockSocket();
        using (var ws2= wrSock2.Socket) {
          var conTsk2= wsMsg.RegisterConnection(ws2, reqTokenSrc2.Token);

          await wsMsg.Publish(tstMsg);
          await wsMsg.Publish(tstMsg, "ignore");   //tstMsg for this scope must not be written to the socket
          Assert.Equal(1, scopeDropCnt);           //drop scope "ignore"
          Assert.Equal(1, wrSock.SendCnt);
          Assert.Equal(1, wrSock2.SendCnt);

          reqTokenSrc.Cancel();             //Simulate a canceled token from HttpContext.RequestAborted
          await wsMsg.Publish(tstMsg);
          Assert.Equal(1, wrSock.SendCnt);
          Assert.True(wrSock.IsAborted);
          Assert.True(conTsk.IsCompleted || conTsk.IsCanceled);
          Assert.False(wrSock2.IsAborted);
          Assert.False(conTsk2.IsCompleted);
          Assert.Equal(2, wrSock2.SendCnt);
          Assert.Equal(1, scopeDropCnt);
          Assert.False(wrSock2.IsDisposed);
        }
      }
      Assert.True(wrSock.IsDisposed);

      wsMsg.Dispose();
      Assert.Equal(2, scopeDropCnt);    //scope "ignore" and "_" must be dropped
    }


    public class MockWebSocket {
      public Mock<WebSocket> MockSocket;
      public WebSocket Socket => MockSocket.Object;
      public WebSocketState SocketSate= WebSocketState.Open;
      public int SendCnt;
      public bool IsAborted;
      public bool IsDisposed;
    }

    public class WriteOnlyMockSocket : MockWebSocket {
      public TaskCompletionSource<ValueWebSocketReceiveResult> TskCmplSrc= new();
      public WriteOnlyMockSocket() {
        var m= this.MockSocket= new Mock<WebSocket>(MockBehavior.Strict);
        m.Setup(sck => sck.State)
         .Returns(() => this.SocketSate);
        m.Setup(sck => sck.SendAsync(It.IsAny<ArraySegment<byte>>(), WebSocketMessageType.Text, true, It.IsAny<CancellationToken>()))
         .Callback(() => ++SendCnt)
         .Returns(() => Task.Delay(10));
        m.Setup(sck => sck.ReceiveAsync(It.IsAny<Memory<byte>>(), It.IsAny<CancellationToken>()))
         .Returns(() => new ValueTask<ValueWebSocketReceiveResult>(TskCmplSrc.Task));
        m.Setup(sck => sck.Abort())
         .Callback(() => this.IsAborted= true);
        m.Setup(sck => sck.Dispose())
         .Callback(() => this.IsDisposed= true);
      }
    }

    public class ReadWriteMockSocket : MockWebSocket {
      public int waitToReceiveCnt;
      public Memory<byte>? MemBuf;
      byte[] msg;
      Memory<byte> msgBuf;
      public TaskCompletionSource ListeningRdySrc= new();               //listening ready
      public TaskCompletionSource<ValueWebSocketReceiveResult> ReceivedSrc;  //received
      CancellationToken tok;
      public ReadWriteMockSocket() {
        var m= this.MockSocket= new Mock<WebSocket>(MockBehavior.Strict);
        m.Setup(sck => sck.State)
         .Returns(() => this.SocketSate);
        m.Setup(sck => sck.SendAsync(It.IsAny<ArraySegment<byte>>(), WebSocketMessageType.Text, true, It.IsAny<CancellationToken>()))
         .Callback(() => ++SendCnt)
         .Returns(() => Task.Delay(10));
        m.Setup(sck => sck.ReceiveAsync(It.IsAny<Memory<byte>>(), It.IsAny<CancellationToken>()))
         .Callback<Memory<byte>, CancellationToken>((buf, tok) => {
           this.tok= tok;
           ++waitToReceiveCnt;
           MemBuf= buf;
           ReceivedSrc= new();
           ListeningRdySrc.TrySetResult();
         })
         .Returns(() => new ValueTask<ValueWebSocketReceiveResult>(ReceivedSrc.Task));
        m.Setup(sck => sck.Abort())
         .Callback(() => this.IsAborted= true);
        m.Setup(sck => sck.Dispose())
         .Callback(() => this.IsDisposed= true);
      }
      public void Receive(string msg) {
        if (null == MemBuf) throw new InvalidOperationException("No pending receive.");
        this.msgBuf= new (this.msg= System.Text.Encoding.UTF8.GetBytes(msg));
        _= Task.Run(async() => {
          while(0 != this.msgBuf.Length) {
            await ListeningRdySrc.Task;
            if (this.tok.IsCancellationRequested) {
              ReceivedSrc.TrySetCanceled();
              return;
            }
            var ln= Math.Min(this.msgBuf.Length, MemBuf.Value.Length);
            this.msgBuf.Slice(0, ln).CopyTo(MemBuf.Value);
            this.msgBuf= this.msgBuf.Slice(ln);
            ListeningRdySrc= new();
            ReceivedSrc.TrySetResult(new ValueWebSocketReceiveResult(ln, WebSocketMessageType.Text, 0 == this.msgBuf.Length));
          }
        });
      }
    }


    class WriteOnlySocket : WebSocket {
      public override WebSocketCloseStatus? CloseStatus => throw new NotImplementedException();

      public override string CloseStatusDescription => throw new NotImplementedException();

      public override WebSocketState State => throw new NotImplementedException();

      public override string SubProtocol => throw new NotImplementedException();

      public override void Abort() {
        throw new NotImplementedException();
      }

      public override Task CloseAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken) {
        throw new NotImplementedException();
      }

      public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken) {
        throw new NotImplementedException();
      }

      public override void Dispose() {
        throw new NotImplementedException();
      }

      public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken) {
        throw new NotImplementedException();
      }

      public override ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken) {
        throw new NotImplementedException();
      }

      public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken) {
        throw new NotImplementedException();
      }
    }

    public class TestMsg {
      public string Body;
    }

    public class TestMsg2 {
      public string Body2;
    }

  }
}