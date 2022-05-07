using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Tlabs.Middleware.Proxy {

  ///<summary>Proxy endpoint definition.</summary>
  public interface IProxyEndpoint {
    ///<summary>Endpoint route template.</summary>
    string EndpointTemplate { get; }
    ///<summary>Proxy URI builder.</summary>
    Func<HttpContext, IDictionary<string, object>, string> ProxyUriBuilder { get; }
    ///<summary>Failure handler (optional).</summary>
    Func<HttpContext, Exception, Task> OnFailure { get; }
  }

  ///<summary>Proxy endpoint implementation.</summary>
  public struct ProxyEndpoint : IProxyEndpoint {
    ///<inheritdoc/>
    public string EndpointTemplate { get; set; }
    ///<inheritdoc/>
    public Func<HttpContext, IDictionary<string, object>, string> ProxyUriBuilder { get; set; }
    ///<inheritdoc/>
    public Func<HttpContext, Exception, Task> OnFailure { get; set; }
  }

}