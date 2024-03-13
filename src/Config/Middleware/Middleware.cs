using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Tlabs.Config {

  ///<summary>Middleware context used with a <see cref="IConfigurator{MiddlewareContext}"/>./>.</summary>
  public class MiddlewareContext {
    ///<summary>Web hosting environment</summary>
    public required IWebHostEnvironment HostingEnv { get; set; }
    ///<summary>Application builder to be configured.</summary>
    public required IApplicationBuilder AppBuilder { get; set; }
  }

  ///<summary>Middleware context used with a <see cref="IConfigurator{MiddlewareContext}"/>./>.</summary>
  public static class MiddlewareContextExt {
    ///<summary>Returns a <see cref="WebApplication"/>.</summary>
    public static WebApplication AsWebApplication(this MiddlewareContext ctx) => (WebApplication)ctx.AppBuilder;
    ///<summary>Returns a <see cref="IEndpointRouteBuilder"/>.</summary>
    public static IEndpointRouteBuilder AsEndpointBuilder(this MiddlewareContext ctx) => (IEndpointRouteBuilder)ctx.AppBuilder;
    ///<summary>Returns a <see cref="IHost"/>.</summary>
    public static IHost AsHost(this MiddlewareContext ctx) => (IHost)ctx.AppBuilder;
  }

  ///<summary>Configurator to add additional assembly path(s).</summary>
  public class WebHostAsmPathConfigurator : AssemblyPathConfigurator<IWebHostBuilder> { }

  ///<summary>Configures debug pages middleware.</summary>
  public class DebugPagesConfigurator : IConfigurator<MiddlewareContext> {
    readonly ILogger log= Tlabs.App.Logger<DebugPagesConfigurator>();

    ///<inheritdoc/>
    public void AddTo(MiddlewareContext mware, IConfiguration cfg) {
      if (mware.HostingEnv.IsDevelopment()) {
        mware.AppBuilder.UseDeveloperExceptionPage();  //see https://docs.microsoft.com/en-us/aspnet/core/fundamentals/error-handling
        //mware.AppBuilder.UseDatabaseErrorPage(); //from Microsoft.AspNetCore.Diagnostics.EntityFrameworkCore
        //mware.AppBuilder.UseBrowserLink(); //see http://vswebessentials.com/features/browserlink
        log.LogInformation("Debug exception page configured");
      }
    }
  }

  ///<summary>Configures ASPNET MVC middleware.</summary>
  public class MvcMiddlewareConfigurator : IConfigurator<MiddlewareContext> {
    readonly ILogger log= Tlabs.App.Logger<MvcMiddlewareConfigurator>();

    ///<inheritdoc/>
    public void AddTo(MiddlewareContext mware, IConfiguration cfg) {
      var appBuilder= mware.AppBuilder;
      appBuilder.UseRouting();
      appBuilder.UseAuthentication();
      appBuilder.UseEndpoints(endppints => endppints.MapControllers());
      log.LogInformation("MVC middleware configured");
    }
  }

}