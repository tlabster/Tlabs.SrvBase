using System;
using System.Linq;
using System.Collections.Generic;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using Tlabs.Middleware.Proxy;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace Tlabs.Config {

  ///<summary>Configures an api proxy.</summary>
  public partial class ApiProxyConfigurator : IConfigurator<MiddlewareContext> {
    IDictionary<string, string> config;
    ILogger log= App.Logger<ApiProxyConfigurator>();

    ///<summary>Default ctor.</summary>
    public ApiProxyConfigurator() : this(null) { }

    ///<summary>Ctor from <paramref name="config"/>.</summary>
    public ApiProxyConfigurator(IDictionary<string, string> config) {
      this.config= config ?? new Dictionary<string, string>();
    }

    ///<inheritdoc/>
    public void AddTo(MiddlewareContext ctx, IConfiguration cfg) {
      var proxEndpoints= config.Select(pair => new ApiProxyEndpoint(pair.Value)).ToList();
      ctx.AppBuilder.UseMvcProxy(proxEndpoints);
      log.LogInformation("{count} MVC proxy routes added to the pipeline.", proxEndpoints.Count);
    }

    class ApiProxyEndpoint : IProxyEndpoint {
      const string DELIM= "::>";
      public ApiProxyEndpoint(string routeDefintion) {
        var splitRoute= routeDefintion.Split(DELIM);
        if (2 != splitRoute.Length) throw new Tlabs.AppConfigException("Invalid route config: " + routeDefintion);

        this.EndpointTemplate= splitRoute[0].Trim();
        var argNames= new List<string>();
        var format= Regex.Replace(splitRoute[1].Trim(), @"{(?<exp>[^}]+)}", match => {
          int idx= argNames.Count;
          argNames.Add(match.Groups["exp"].Value);
          return "{" + idx.ToString() + "}";
        });

        this.ProxyUriBuilder= (ctx, dict) => {
          var args= argNames.Select(name => dict[name]).ToArray();
          return string.Format(format, args);
        };
      }

      ///<inheritdoc/>
      public string EndpointTemplate { get; }
      ///<inheritdoc/>
      public Func<HttpContext, IDictionary<string, object>, string> ProxyUriBuilder { get; }
      ///<inheritdoc/>
      public Func<HttpContext, Exception, Task> OnFailure { get; }

    }


  }

}