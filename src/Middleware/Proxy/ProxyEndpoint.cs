using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Tlabs.Middleware.Proxy {

  ///<summary>Proxy endpoint definition.</summary>
  public struct ProxyEndpoint {
    ///<summary>Endpoint route template.</summary>
    public string EndpointTemplate;
    ///<summary>Proxy URI builder.</summary>
    public Func<HttpContext, IDictionary<string, object>, string> ProxyUriBuilder;
    ///<summary>Failure handler (optional).</summary>
    public Func<HttpContext, Exception, Task> OnFailure;

  }
}