using System.Collections.Generic;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.Configuration;

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
    public KestrelConfigurator(IDictionary<string, string> config) {
      this.config= config ?? new Dictionary<string, string>();
    }

    ///<summary>Adds the Kestrel configuration to the <paramref name="target"/>.</summary>
    public void AddTo(IWebHostBuilder target, IConfiguration cfg) {
      string cfgVal;
      target.UseKestrel(opt => {
        opt.AddServerHeader= false;
        if(config.TryGetValue(CERTFILE_KEY, out cfgVal)) {
          string certPwd= null;
          if (config.TryGetValue(CERTPWD_KEY, out certPwd))
            opt.ListenAnyIP(443, lop => lop.UseHttps(cfgVal, certPwd));
          else
            opt.ListenAnyIP(443, lop => lop.UseHttps(cfgVal));
        }
      });

      if (config.TryGetValue(URLS_KEY, out cfgVal))
        target.UseUrls(cfgVal);
      /*  Apply webHosting configuration before IIS integration!
       */
      target.UseConfiguration(cfg)
            .UseIISIntegration();
    }
  }
}