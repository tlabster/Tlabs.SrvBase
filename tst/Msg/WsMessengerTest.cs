using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using Tlabs.Sync;
using Tlabs.Data.Serialize;

using Xunit;
using Xunit.Abstractions;
using Moq;

namespace Tlabs.Msg.Intern.Test {


  public class WsMessengerTest : IClassFixture<WsMsgTestContext> {

    WsMsgTestContext tstCtx;
    ITestOutputHelper tstout;

    public WsMessengerTest(WsMsgTestContext tstCtx, ITestOutputHelper tstout) {
      this.tstCtx= tstCtx;
      this.tstout= tstout;
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
        await wsMsg2.Publish(tstMsg2);
        Assert.Equal(2, wrSock.SendCnt);          //tstMsg & tstMsg2 published to same socket

        reqTokenSrc.Cancel();             //Simulate a canceled token from HttpContext.RequestAborted
        await wsMsg.Publish(tstMsg);
        Assert.Equal(2, wrSock.SendCnt);  //no more msg published
        Assert.True(wrSock.IsAborted);
        Assert.False(wrSock.IsDisposed);
        Assert.True(conTsk.IsCompleted || conTsk.IsCanceled);
        Assert.Equal(1, scopeDropCnt);
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
      // var tstMsg= new TestMsg();
      byte[] recBuf= null;
      string recScope= null;
      TaskCompletionSource tcs= new();
      using (var ws= rwSock.Socket) {
        var conTsk= wsMsg.RegisterConnection(ws, reqTokenSrc.Token, "_", (buf, scope) => { recBuf= buf; recScope= scope; tcs.TrySetResult(); });
        Assert.Equal(1, rwSock.ReceiveCnt);
        Assert.Null(recBuf);

        await Task.Yield();

        rwSock.Receive("{  }");
        await tcs.Task;
        Assert.Equal("_", recScope);
        Assert.Equal(2, rwSock.ReceiveCnt);

        tcs= new();
        recBuf= null;
        recScope= null;
        reqTokenSrc.Cancel();             //Simulate a canceled token from HttpContext.RequestAborted

        rwSock.Receive("{ x }");
        await Assert.ThrowsAnyAsync<TimeoutException>(() => tcs.Task.Timeout(1000)); //nothing received with canceled reqTokenSrc
        Assert.Null(recScope);
        Assert.Equal(2, rwSock.ReceiveCnt);   //no more received

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
      wsMsg.DroppedScope+= scope => ++scopeDropCnt;

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
          Assert.Equal(0, scopeDropCnt);
          Assert.False(wrSock2.IsDisposed);
        }
      }
      Assert.True(wrSock.IsDisposed);

      wsMsg.Dispose();
      Assert.Equal(1, scopeDropCnt);
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
      public TaskCompletionSource<WebSocketReceiveResult> TskCmplSrc= new();
      public WriteOnlyMockSocket() {
        var m= this.MockSocket= new Mock<WebSocket>(MockBehavior.Strict);
        m.Setup(sck => sck.State)
         .Returns(() => this.SocketSate);
        m.Setup(sck => sck.SendAsync(It.IsAny<ArraySegment<byte>>(), WebSocketMessageType.Text, true, It.IsAny<CancellationToken>()))
         .Callback(() => ++SendCnt)
         .Returns(() => Task.Delay(10));
        m.Setup(sck => sck.ReceiveAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<CancellationToken>()))
         .Returns(() => TskCmplSrc.Task);
        m.Setup(sck => sck.Abort())
         .Callback(() => this.IsAborted= true);
        m.Setup(sck => sck.Dispose())
         .Callback(() => this.IsDisposed= true);
      }
    }

    public class ReadWriteMockSocket : MockWebSocket {
      public int ReceiveCnt;
      public ArraySegment<byte> Buf;
      public TaskCompletionSource<WebSocketReceiveResult> TskCmplSrc;
      public ReadWriteMockSocket() {
        var m= this.MockSocket= new Mock<WebSocket>(MockBehavior.Strict);
        m.Setup(sck => sck.State)
         .Returns(() => this.SocketSate);
        m.Setup(sck => sck.SendAsync(It.IsAny<ArraySegment<byte>>(), WebSocketMessageType.Text, true, It.IsAny<CancellationToken>()))
         .Callback(() => ++SendCnt)
         .Returns(() => Task.Delay(10));
        m.Setup(sck => sck.ReceiveAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<CancellationToken>()))
         .Callback<ArraySegment<byte>, CancellationToken>((buf, tok) => {
           ++ReceiveCnt;
           Buf= buf;
           TskCmplSrc= new();
         })
         .Returns(() => TskCmplSrc.Task);
        m.Setup(sck => sck.Abort())
         .Callback(() => this.IsAborted= true);
        m.Setup(sck => sck.Dispose())
         .Callback(() => this.IsDisposed= true);
      }
      public void Receive(string msg) {
        if (null == Buf) throw new InvalidOperationException("No pending receive.");
        var binMsg= System.Text.Encoding.UTF8.GetBytes(msg);
        Array.Copy(binMsg, Buf.Array, binMsg.Length);
        TskCmplSrc.TrySetResult(new WebSocketReceiveResult(binMsg.Length, WebSocketMessageType.Text, true));
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