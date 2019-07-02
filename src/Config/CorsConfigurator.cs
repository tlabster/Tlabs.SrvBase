using System;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;

namespace Tlabs.Config {
  ///<summary>Configures a CORS policy.</summary>
  ///<remarks>
  ///<para>TIP: Test CORS using: https://www.test-cors.org/</para>
  ///1. Simply configure a global CORS policy for all enddpoints under 'applicationMiddleware' section:
  ///<code>
  ///  "CORS": {
  ///    "ord": 30,
  ///    "type": "Tlabs.Config.CorsConfigurator, Tlabs.SrvBase",
  ///    "options": {
  ///      "PreflightMaxAge": 120,
  ///      "AllowAnyHeader": true,
  ///      "AllowedOrigins": ["http://localhost:8086", "http://*.pol-it.de"]
  ///     }
  ///  }
  ///</code> 
  ///2. Configure a named CORS policy with section 'applicationServices'
  ///<code>
  ///  "applicationServices": {
  ///    "CORS": {
  ///      "type": "Tlabs.Config.CorsConfigurator, Tlabs.SrvBase",
  ///      "options": {
  ///      "PreflightMaxAge": 120,
  ///      "AllowAnyHeader": true,
  ///      "AllowedOrigins": ["http://localhost:8086", "http://*.pol-it.de"]
  ///    }
  ///  },
  ///</code>
  ///... and use this policy with the attribut {EnableCors] in your controller:
  ///<code>
  /// [Route("api/[controller]")]
  /// [ApiController]
  /// public class WidgetController : ControllerBase {
  /// // GET api/values
  /// [EnableCors("TlabsCORSappPolicy")]
  /// [HttpGet]
  /// public string Get() { ... }
  ///</code>
  ///</remarks>
  public class CorsConfigurator : IConfigurator<IServiceCollection>, IConfigurator<MiddlewareContext>  {
    static readonly ILogger log= Tlabs.App.Logger<CorsConfigurator>();

    ///<summary>CORS policy name.</summary>
    const string TlabsCORSappPolicy= nameof(TlabsCORSappPolicy);
    ///<inherit/>
    public void AddTo(IServiceCollection services, IConfiguration cfg) {
      var polOpt= new PolicyOptions();
      var optCfg= cfg.GetSection("options");

      if (optCfg.GetChildren().Any()) optCfg.Bind(polOpt);
      else log.LogWarning($"Missing CORS 'options' section - using default...");
      services.AddCors(options => options.AddPolicy(TlabsCORSappPolicy, builder => polOpt.Configure(builder)));
      log.LogInformation($"CORS policy '{TlabsCORSappPolicy}' configured from options.");
    }

    ///<inherit/>
    public void AddTo(MiddlewareContext mware, IConfiguration cfg) {
      PolicyOptions polOpt= null;
      var optCfg= cfg.GetSection("options");
      if (optCfg.GetChildren().Any()) {
        polOpt= new PolicyOptions();
        optCfg.Bind(polOpt);
        mware.AppBuilder.UseCors(builder => polOpt.Configure(builder));
        log.LogInformation("CORS policy configured from options.");
      }
      else mware.AppBuilder.UseCors(TlabsCORSappPolicy);
      log.LogInformation($"CORS policy globally appliad to all endpoints.");
    }

    ///<summary>CORS policy options.</summary>
    public class PolicyOptions {
      ///<summary>Default ctor.</summary>
      public PolicyOptions() {
        PreflightMaxAge= 300; //5 minutes
        AllowWildcardSubdomain= true;
      }
      ///<summary>Max. time in seconds a preflight request can be cached.</summary>
      public int PreflightMaxAge { get; set; }
      ///<summary>True to allow any header.</summary>
      public bool AllowAnyHeader { get; set; }
      ///<summary>True to allow any method.</summary>
      public bool AllowAnyMethod { get; set; }
      ///<summary>True to allow any origin.</summary>
      public bool AllowAnyOrigin { get; set; }
      ///<summary>True to allow access to any credentials.</summary>
      public bool AllowCredentials { get; set; }
      ///<summary>True enable wildcard subdomain match with <ssee cref="AllowedOrigins"/>.</summary>
      public bool AllowWildcardSubdomain { get; set; }
      ///<summary>List of allowed origin(s).</summary>
      public string[] AllowedOrigins { get; set; }
      ///<summary>List of allowed header(s)</summary>
      public string[] AllowedHeaders { get; set; }
      ///<summary>List of exposed header(s)</summary>
      public string[] AllowedExposedHeaders { get; set; }
      ///<summary>List of allowed method(s)</summary>
      public string[] AllowedMethods { get; set; }

      ///<summary>Configure CORS policy</summary>
      public CorsPolicyBuilder Configure(CorsPolicyBuilder pb) {
        pb.SetPreflightMaxAge(new TimeSpan(0, 0, PreflightMaxAge));
        
        if (AllowWildcardSubdomain) pb.SetIsOriginAllowedToAllowWildcardSubdomains();
        
        if (AllowCredentials) pb.AllowCredentials();
        else pb.DisallowCredentials();
        
        if (AllowAnyHeader) pb.AllowAnyHeader();
        else if (null != AllowedHeaders) pb.WithHeaders(AllowedHeaders);
        if (null != AllowedExposedHeaders) pb.WithExposedHeaders(AllowedExposedHeaders);

        if (AllowAnyMethod) pb.AllowAnyMethod();
        else if (null != AllowedMethods) pb.WithHeaders(AllowedMethods);

        if (AllowAnyOrigin) pb.AllowAnyOrigin();
        else if (null != AllowedOrigins) pb.WithHeaders(AllowedOrigins);

        return pb;
      }
    }

  }
}