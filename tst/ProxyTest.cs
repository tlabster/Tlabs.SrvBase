using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using Xunit;
using Xunit.Abstractions;

using Tlabs.Config;

namespace Tlabs.Middleware.Proxy.Test {

  public class TestController : Controller {

    [HttpPost("V3/BasketPoint/Service/BasketPoint.svc/BasketPoint")]
    public ActionResult GetBasketPoints() {
      Console.WriteLine("MVC controller: V3/BasketPoint/Service/BasketPoint.svc/BasketPoint");
      var req= HttpContext.GetProxyRequestMessage();
      if (null == req)
        return StatusCode(500,"No 'proxyMsg' set with HttpContext");

      var q= HttpUtility.ParseQueryString(req.RequestUri.Query);
      if (q["param"] != "test") return StatusCode(400, "param missing in query string");

      var str= req.Content.ReadAsStringAsync().GetAwaiter().GetResult();
      if ("{ }" != str) return StatusCode(400, "Bad request content.");

      if (null == HttpContext.GetProxyResponseMessage())
        return StatusCode(500, "No 'proxyResp' set with HttpContext");

      return StatusCode(418, "Teapot response from basket controller");
    }

    [HttpGet("api/{*all}")]
    public ActionResult GeneralAPI() {
      return Ok("API");
    }
  }

  public class ProxyTest : IClassFixture<ProxyTest.ProxyTestFixture> {

    public class ProxyTestFixture {
        public readonly TestServer Server;
        public readonly HttpClient Client;

      public ProxyTestFixture() {
        this.Server= new TestServer(new WebHostBuilder().UseStartup<ProxyTest.Startup>());
        this.Client= this.Server.CreateClient();
      }
    }

    public class Startup {
      public void ConfigureServices(IServiceCollection services) {
        services.AddRouting();
        services.AddMvc();
        services.AddApiClient();
      }

      public void Configure(IApplicationBuilder app, IHostingEnvironment env) {

        var proxyCfg= new ApiProxyConfigurator(new Dictionary<string, string> {
          /** Match all PC APIs but Member & Promotion
           */
          ["someRoute"]= "V{svcVers}/{svcCat}/Service/{svcName:regex(^(?!Member|Promotion).+$)}.svc/{svcEnd} ::> https://sandbox.prime-cloud.com/V{svcVers}/{svcCat}/Service/{svcName}.svc/{svcEnd}"
        });
        proxyCfg.AddTo(new MiddlewareContext { AppBuilder= app }, null);

        app.UseMvc();

      }
    }

    public static class ProxyRouteMethods {

      [ProxyRoute("api/posts/tostring/{postId}")]
      public static string ProxyToString(int postId) {
        return $"https://jsonplaceholder.typicode.com/posts/{postId}";
      }

      //[ProxyRoute("http://int.prime-cloud.com/V3/BasketPoint/Service/BasketPoint.svc/BasketPoint")]
      [ProxyRoute("V{svcVers}/{svcCat}/Service/{svcName}.svc/{svcEnd}")]
      public static string ProxyToPcApi(int svcVers, string svcCat, string svcName, string svcEnd) {
        return $"http://int.prime-cloud.com/V{svcVers}/{svcCat}/Service/{svcName}.svc/{svcEnd}";
      }
    }


    private ProxyTestFixture testFixture;
    private readonly ITestOutputHelper tstout;

    public ProxyTest(ProxyTestFixture testFixture, ITestOutputHelper output) {
      this.testFixture= testFixture;
      this.tstout= output;
    }


    [Fact]
    public async Task SimpleApi() {
      var response= await testFixture.Client.GetAsync("api/ok/check/this"); ///ok/check/this
      Assert.True(response.IsSuccessStatusCode);
      Assert.StartsWith("text/", response.Content.Headers.ContentType.MediaType);

      var respStr= await response.Content.ReadAsStringAsync();
      Assert.Equal("API", respStr);
    }

    [Fact]
    public async Task ProxyBasketApi() {
      var req= new HttpRequestMessage(HttpMethod.Post, "V3/BasketPoint/Service/BasketPoint.svc/BasketPoint?param=test&other=more");
      req.Content= new StringContent("{ }", System.Text.Encoding.UTF8, "application/json");
      var response= await testFixture.Client.SendAsync(req);
      // var response= await testFixture.Client.GetAsync("V3/BasketPoint/Service/BasketPoint.svc/BasketPoint?param=test&other=more");
      Assert.Equal(418, (int)response.StatusCode);  //handled by TestController.GetBasketPoints() ??

      var respStr= await response.Content.ReadAsStringAsync();
      Assert.Contains("Teapot response", respStr);
    }

    [Fact]
    public async Task ProxyEarnApi() {
      var response= await testFixture.Client.GetAsync("V3/Earn/Service/Earn.svc/Earn");
      Assert.Equal(400, (int)response.StatusCode);

      //check for some PrimeCloud specific response properties:
      Assert.Equal("text/html", response.Content.Headers.ContentType.MediaType);
      Assert.StartsWith("Microsoft-IIS", response.Headers.Server.ToString());
      Assert.True(response.Headers.Contains("X-AspNet-Version"));

      var respStr= await response.Content.ReadAsStringAsync();
      Assert.Contains("<title>Request Error</title>", respStr);
    }
  }
}