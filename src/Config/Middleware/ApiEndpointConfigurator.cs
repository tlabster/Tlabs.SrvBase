using System;
using System.Reflection;
using System.Linq;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using Tlabs.Dynamic;

namespace Tlabs.Config {

  ///<summary>API end-point configurator non-generic base class.</summary>
  public class ApiEndpointConfigurator { }

  ///<summary>API end-point configurator.</summary>
  public class ApiEndpointConfigurator<T> : ApiEndpointConfigurator, IConfigurator<MiddlewareContext> {
    static readonly string[] DEFAULT_METHOD= new string[] { "GET" };
    static readonly ILogger log= Tlabs.App.Logger<ApiEndpointConfigurator>();

    internal class EndpointDescriptor {
      public string? path { get; set; }
      public string? name { get; set; }
      public string[] method { get; set; }= DEFAULT_METHOD;
      public string? action { get; set; }
      public bool anonymous { get; set; }= false;
    }


    /* Support:
     * - GroupEndpoint
     * - RequireAuthorization() https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.builder.authorizationendpointconventionbuilderextensions.requireauthorization?view=aspnetcore-8.0

     */
    ///<inheritdoc/>
    public void AddTo(MiddlewareContext midCtx, IConfiguration cfg) {
      var builder= (IEndpointRouteBuilder)midCtx.AppBuilder;
      var epDesc= new EndpointDescriptor() { path= (cfg as IConfigurationSection)?.Key };
      cfg.Bind(epDesc);
      if (null == epDesc.path) throw new AppConfigException("No API end-point path");

      var map= builder.MapMethods(epDesc.path, epDesc.method.Distinct(), actionDelegateBuilder(epDesc));
      if (epDesc.anonymous) map.AllowAnonymous();
    }

    static Delegate actionDelegateBuilder(EndpointDescriptor epDesc) {
      if (null == epDesc.action) throw new AppConfigException("No API end-point action");

      var handlerType= typeof(T);
      // var staticHandlerType= handlerType.IsAbstract && handlerType.IsSealed;
      // if (!staticHandlerType) throw new NotSupportedException($"Only static API end-point handler ({handlerType.Name}) supported");
      var actionMethod= handlerType.GetMethod(epDesc.action, BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static) ?? throw new AppConfigException($"No (static) '{epDesc.action}' action method found in {handlerType.Name}");

      /* NOTE to support non static action-methods:
       * This would require to dynamically create a expression lamda delegate of the form:
       *    Func<TactionMethodParameter,... ,TinstanceType, TactionMethodReturnType>
       * that actualy invokes the actionMethod of the target instance.
       * While the creation of such an expression lamda would be basically feasible,
       * the resulting delegate would be missing the original attributes on the actionMethod like:
       *    [AllowAnonymous]
            int public ActionMehtod( [FromRoute] int id, [FromQuery(Name="p")] int page ) => 123;
       * since adding attributes to an Expression.Parameter(type, name) does not seem to be support by now???
       */

      var actionDelegate= actionMethod.AsDelegate();
      log.LogInformation("{path} API end-point added", epDesc.path);

      var param= actionMethod.GetParameters().First();
      return actionDelegate;
    }
  }










#if false

MethodInfo mi = genericType.GetMethod("DoSomething",
                                BindingFlags.Instance | BindingFlags.Public);

var p1 = Expression.Parameter(genericType, "generic");
var p2 = Expression.Parameter(fieldType, "instance");
var func = typeof (Func<,,>);
var genericFunc = func.MakeGenericType(genericType, fieldType, typeof(int));
var x = Expression.Lambda(genericFunc, Expression.Call(p1, mi, p2),
                new[] { p1, p2 }).Compile();

#endif






}