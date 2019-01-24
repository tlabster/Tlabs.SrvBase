using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyModel;

namespace Tlabs.Middleware.Proxy {

    ///<summary>Proxy builder extensions for a <see cref="IApplicationBuilder"/>.</summary>
    public static class ProxyBuilderExtensions {

    internal static IEnumerable<Assembly> GetReferencingAssemblies() {
      var dependencies= DependencyContext.Default.RuntimeLibraries;
      foreach (var library in dependencies) {
        Assembly assembly;
        try { assembly= Assembly.Load(new AssemblyName(library.Name)); }
        catch (Exception) { assembly= null; }
        if (null != assembly) yield return assembly;
      }
    }

    /// <summary>Use middleware to proxy all endpoints that match any routes defined with static methods marked with a [<see cref="ProxyRouteAttribute">ProxyRoute</see>] attribute.</summary>
    public static void UseMvcProxy(this IApplicationBuilder app) {
      var methods= GetReferencingAssemblies().SelectMany(a => a.GetTypes())
                                              .SelectMany(t => t.GetMethods())
                                              .Where(m => m.GetCustomAttributes(typeof(ProxyRouteAttribute), false).Length > 0);

      var proxyEndpoints= methods.Select<MethodInfo, ProxyEndpoint>(method => {
        var name= method.Name;
        var attribute= method.GetCustomAttributes(typeof(ProxyRouteAttribute), false).Single() as ProxyRouteAttribute;
        var parameters= method.GetParameters();

        if (method.ReturnType != typeof(string)) throw new InvalidOperationException($"Proxy address generator method ({name}) must return a 'string'.");
        if (!method.IsStatic) throw new InvalidOperationException($"Proxy address generator method ({name}) must be static.");

        return new ProxyEndpoint {
          EndpointTemplate= attribute.Route,
          ProxyUriBuilder= (context, args) => {
            if (args.Count() != parameters.Count()) throw new InvalidOperationException($"Proxy address generator method ({name}) parameter mismatch.");

            var castedArgs= args.Zip(parameters, (a, p) => new { Val= a.Value.ToString(), Type= p.ParameterType, Name= p.Name })
                                .Select(z => {
              try {
                return TypeDescriptor.GetConverter(z.Type).ConvertFromString(z.Val);
              }
              catch (Exception) {
                throw new InvalidOperationException($"Proxy address generator method ({name}): Parameter {z.Name} cannot be casted to {z.Type.FullName}.");
              }
            });

            return method.Invoke(null, castedArgs.ToArray()) as string;
          }
        };
      });

      app.UseMvcProxy(proxyEndpoints);
    }

    /// <summary>Use middleware to proxy all <paramref name="proxyEndpoints"/>.</summary>
    public static void UseMvcProxy(this IApplicationBuilder app,
                                   IEnumerable<ProxyEndpoint> proxyEndpoints)
    {
      var http= app.ApplicationServices.GetRequiredService<HttpClient>();
      app.UseRouter(router => {
        foreach (var proxy in proxyEndpoints) {
          router.MapMiddlewareRoute(proxy.EndpointTemplate, proxyApp => {
            proxyApp.Use(async (httpCtx, next) => {
              string proxUri= "??";
              try {
                proxUri= proxy.ProxyUriBuilder(httpCtx, httpCtx.GetRouteData().Values);
                var proxResp= httpCtx.TransferProxyMessageAsync(http, proxUri);
                await next();
                await httpCtx.ReturnProxyResponse(proxResp);
              }
              catch (Exception e) {
                if (proxy.OnFailure != null) {
                  await proxy.OnFailure(httpCtx, e);
                  return;
                }
                // If the failures are not caught, then write a generic response.
                httpCtx.Response.StatusCode= 500;
                await httpCtx.Response.WriteAsync($"Request could not be proxied to: '{proxUri}'.\n\n{e.Message}\n\n{e.StackTrace}.");
              }
            });
            proxyApp.UseMvc();
          });
        }
      });
    }

  }
}