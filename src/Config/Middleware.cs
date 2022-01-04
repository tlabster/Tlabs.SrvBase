using System.IO;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.FileProviders;

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


  ///<summary>Configures static file middleware.</summary>
  public class StaticContentConfigurator : IConfigurator<MiddlewareContext> {
    private ILogger log;
    IDictionary<string, string> config;
    ///<summary>Default ctor.</summary>
    public StaticContentConfigurator() : this(null)  { }

    ///<summary>Ctor from <paramref name="config"/>.</summary>
    public StaticContentConfigurator(IDictionary<string, string> config) {
      this.config= config ?? new Dictionary<string, string>(0);
      this.log= App.Logger<StaticContentConfigurator>();
    }
    ///<inherit/>
    public void AddTo(MiddlewareContext mware, IConfiguration cfg) {

      /* Configure default page(s):
       * (This MUST come before UseStaticFiles()...)
       */
      string dfltPage;
      if (config.TryGetValue("defaultPage", out dfltPage) && !string.IsNullOrEmpty(dfltPage)) {
        string[] dfltPages= dfltPage.Split(',');
        log.LogInformation("Default page(s) are: '{defaultPages}'", string.Join(",", dfltPages));
        mware.AppBuilder.UseDefaultFiles(new DefaultFilesOptions() {
          DefaultFileNames= dfltPages
        });
      }

      mware.AppBuilder.UseStaticFiles(); //from HostingEnv.WebRootPath
      log.LogInformation("Serving static content from: '{webroot}'", mware.HostingEnv.WebRootPath);

      foreach( var pair in config.Where(p => p.Key.StartsWith("/"))) {
        var contPath= Path.Combine(App.ContentRoot, pair.Value);
        if (!Directory.Exists(contPath)) {
          log.LogDebug("Ignoring non exisiting static content path: {path}", contPath);
          continue; //UseStaticFiles would throw on non exisiting path
        }
        mware.AppBuilder.UseStaticFiles(new StaticFileOptions() {
          RequestPath= new PathString(pair.Key),
          FileProvider= new PhysicalFileProvider(contPath)
        });
        log.LogInformation("Serving static content from {path}: '{physical}'", pair.Key, pair.Value);
      }
    }
  }

  ///<summary>Configures debug pages middleware.</summary>
  public class DebugPagesConfigurator : IConfigurator<MiddlewareContext> {

    ///<inherit/>
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

    ///<inherit/>
    public void AddTo(MiddlewareContext mware, IConfiguration cfg) {
      var appBuilder= mware.AppBuilder;
      appBuilder.UseRouting();
      appBuilder.UseAuthentication();
      appBuilder.UseEndpoints(endppints => endppints.MapControllers());
    }
  }

}