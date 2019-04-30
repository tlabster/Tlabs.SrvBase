using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;

namespace Tlabs.Middleware.Proxy {

  ///<summary>Http proxy extension helpers.</summary>
  public static class HttpExtensions {
    const string PROX_MSG= nameof(Tlabs.Middleware.Proxy) + ".reqMsg";
    const string PROX_RESP= nameof(Tlabs.Middleware.Proxy) + ".respMsgTsk";

    internal static async Task<HttpRequestMessage> ToRequestMessage(this HttpRequest req, string uriString, CancellationToken ct) {
      uriString=   string.IsNullOrEmpty(uriString)
                 ? req.GetEncodedUrl()
                 : uriString + req.QueryString.ToUriComponent();
      var reqUri= new Uri(uriString);
      var reqMsg= new HttpRequestMessage(new HttpMethod(req.Method), reqUri);
      var reqMethod= req.Method;
      if (   !HttpMethods.IsGet(reqMethod)
          && !HttpMethods.IsHead(reqMethod)
          && !HttpMethods.IsDelete(reqMethod)
          && !HttpMethods.IsTrace(reqMethod)) {
        var memStrm= new MemoryStream((int)(req.ContentLength ?? 4096));
        await req.Body.CopyToAsync(memStrm, ct);
        memStrm.Position= 0;
        reqMsg.Content= new StreamContent(memStrm);
      }
      // Copy the request headers.
      foreach (var header in req.Headers)
        if (!reqMsg.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()))
          reqMsg.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());

      reqMsg.Headers.Host= reqMsg.RequestUri.Authority;
      return reqMsg;
    }

    ///<summary>Returns the proxied <see cref="HttpRequestMessage"/> or <c>null</c> if no request route was proxied.</summary>
    public static HttpRequestMessage GetProxyRequestMessage(this HttpContext ctx) => ctx.Items[PROX_MSG] as HttpRequestMessage;

    ///<summary>Returns the (pending) proxied <see cref="Task{HttpResponseMessage}"/> or <c>null</c> if no route was proxied.</summary>
    public static Task<HttpResponseMessage> GetProxyResponseMessage(this HttpContext ctx) => ctx.Items[PROX_RESP] as Task<HttpResponseMessage>;

    ///<summary>Dispose off any proxy messages from the <see cref="HttpContext"/>.</summary>
    public static void DisposeProxyMessage(this HttpContext ctx) {
      ctx.GetProxyRequestMessage()?.Dispose();
      var respTsk= ctx.GetProxyResponseMessage();
      if (null != respTsk && respTsk.IsCompletedSuccessfully)
        respTsk.Result.Dispose();
    }

    internal static async Task<HttpRequestMessage> CreateProxyRequest(this HttpContext ctx, string uriString= null) {
      var reqMsg= await ctx.Request.ToRequestMessage(uriString, ctx.RequestAborted);
      ctx.Items[PROX_MSG]= reqMsg;
      return reqMsg;
    }

    internal static async Task ReturnResponseMessage(this HttpResponse resp, HttpResponseMessage respMsg) {
      resp.StatusCode= (int)respMsg.StatusCode;

      foreach (var hdr in respMsg.Headers)
        resp.Headers[hdr.Key]= hdr.Value.ToArray();
      foreach (var hdr in respMsg.Content.Headers)
        resp.Headers[hdr.Key]= hdr.Value.ToArray();
      resp.Headers.Remove("transfer-encoding");

      await respMsg.Content.CopyToAsync(resp.Body);
    }

    internal static async Task<HttpResponseMessage> TransferProxyMessageAsync(this HttpContext ctx, HttpClient http, string uriString) {
      var proxRespTsk= http.SendAsync(await ctx.CreateProxyRequest(uriString),
                                      HttpCompletionOption.ResponseHeadersRead,
                                      ctx.RequestAborted);
      ctx.Items[PROX_RESP]= proxRespTsk;
      return await proxRespTsk;
    }

    internal static async Task ReturnProxyResponse(this HttpContext ctx, Task<HttpResponseMessage> respMsg= null) {
      if (null == (respMsg= respMsg ?? ctx.GetProxyResponseMessage())) throw new InvalidOperationException("No proxy response");

      if (!ctx.Response.HasStarted)
        await ctx.Response.ReturnResponseMessage(await respMsg);
    }
  }
}