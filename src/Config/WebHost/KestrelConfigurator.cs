using System.Collections.Generic;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Tlabs.Config {

  ///<summary>Kestrel HTTP server configurator</summary>
  ///<remarks>
  ///Supported config properties:
  ///<list type="table">
  ///<item>
  /// <term>urls</term>
  /// <description> semicolon (;) separated list of URL prefixes</description>
  ///</item>
  ///<item>
  /// <term>httpsCertFile</term>
  /// <description>HTTPS X509 certificate file</description>
  ///</item>
  ///<item>
  /// <term>httpsCertPwd</term>
  /// <description>password to access X509 certificate file</description>
  ///</item>
  ///</list>
  ///</remarks>
  public class KestrelConfigurator : IConfigurator<IWebHostBuilder> {
    private readonly IDictionary<string, string> config;

    ///<summary>urls config key</summary>
    public const string URLS_KEY= "urls";
    ///<summary>httpsCertFile config key</summary>
    public const string CERTFILE_KEY= "httpsCertFile";
    ///<summary>httpsCertPwd config key</summary>
    public const string CERTPWD_KEY= "httpsCertPwd";

    ///<summary>Default ctor</summary>
    public KestrelConfigurator() : this(null) { }

    ///<summary>Ctor from <paramref name="config"/> dictionary</summary>
    public KestrelConfigurator(IDictionary<string, string>? config) {
      this.config= config ?? new Dictionary<string, string>();
    }

    ///<summary>Adds the Kestrel configuration to the <paramref name="builder"/>.</summary>
    public void AddTo(IWebHostBuilder builder, IConfiguration cfg) {
      builder.ConfigureServices(services => {
        var optConfig= cfg.GetSection("options");
        services.Configure<KestrelServerOptions>(optConfig);
      });
      builder.UseKestrel(opt => {
#if BROKEN
        var optConfig= cfg.GetSection("options");
        opt.Configure(optConfig).Load();      //this does not work
#endif
        opt.AllowSynchronousIO= true;
        opt.AddServerHeader= false;
        if (config.TryGetValue(CERTFILE_KEY, out var cfgVal)) {
          if (config.TryGetValue(CERTPWD_KEY, out var certPwd))
            opt.ListenAnyIP(443, lop => lop.UseHttps(cfgVal, certPwd));
          else
            opt.ListenAnyIP(443, lop => lop.UseHttps(cfgVal));
        }
      });

      builder.UseIISIntegration();
    }
  }
}