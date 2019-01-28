using System;

namespace Tlabs.Middleware.Proxy {
  /// <summary>
  ///  This attribute indicates a static method returning a URI to which a request will be proxied.
  /// </summary>
  [AttributeUsage(AttributeTargets.Method)]
  public class ProxyRouteAttribute : Attribute {
    /// <summary>The local route to be proxied.</summary>
    public string Route { get; set; }

    /// <summary>Ctor from <paramref name="route"/></summary>
    /// <param name="route">The local route to be proxied.</param>
    public ProxyRouteAttribute(string route) {
      this.Route= route;
    }
  }
}