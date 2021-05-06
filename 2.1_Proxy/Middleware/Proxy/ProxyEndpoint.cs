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
    ///<inherit/>
    public string EndpointTemplate { get; set; }
    ///<inherit/>
    public Func<HttpContext, IDictionary<string, object>, string> ProxyUriBuilder { get; set; }
    ///<inherit/>
    public Func<HttpContext, Exception, Task> OnFailure { get; set; }
  }

}