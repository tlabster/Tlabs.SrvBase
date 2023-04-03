using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Tlabs.Config {

  ///<summary>Middleware context used with a <see cref="IConfigurator{MiddlewareContext}"/>./>.</summary>
  public class MiddlewareContext {
    ///<summary>Web hosting environment</summary>
    public IWebHostEnvironment HostingEnv { get; set; }
    ///<summary>Application builder to be configured.</summary>
    public IApplicationBuilder AppBuilder { get; set; }
  }

  ///<summary>Configurator to add additional assembly path(s).</summary>
  public class WebHostAsmPathConfigurator : AssemblyPathConfigurator<IWebHostBuilder> { }

  ///<summary>Configures debug pages middleware.</summary>
  public class DebugPagesConfigurator : IConfigurator<MiddlewareContext> {

    ///<inheritdoc/>
    public void AddTo(MiddlewareContext mware, IConfiguration cfg) {
      if (mware.HostingEnv.IsDevelopment()) {
        mware.AppBuilder.UseDeveloperExceptionPage();  //see https://docs.microsoft.com/en-us/aspnet/core/fundamentals/error-handling
        //mware.AppBuilder.UseDatabaseErrorPage(); //from Microsoft.AspNetCore.Diagnostics.EntityFrameworkCore
        //mware.AppBuilder.UseBrowserLink(); //see http://vswebessentials.com/features/browserlink
      }
    }
  }

  ///<summary>Configures ASPNET MVC middleware.</summary>
  public class MvcMiddlewareConfigurator : IConfigurator<MiddlewareContext> {
    ILogger log= Tlabs.App.Logger<MvcMiddlewareConfigurator>();

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