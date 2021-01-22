using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tlabs;
using Tlabs.Config;
using Tlabs.Data;
using Tlabs.Data.Entity;
using Tlabs.Server.Model;

namespace Tlabs.Server.Auth {
  ///<summary>Filter that </summary>
  public class RoleDefaultParamsFilter : IActionFilter {
    private static readonly ILogger log= Tlabs.App.Logger<RoleDefaultParamsFilter>();
    ///<inheritdoc/>
    public void OnActionExecuted(ActionExecutedContext context) {
      // Empty
    }

    ///<inheritdoc/>
    public void OnActionExecuting(ActionExecutingContext ctx) {
      //Validate??
      var forcedParams= paramsForRole(ctx);
      if (null == forcedParams) return;

      // Get parameter name from role
      if (ctx.ActionDescriptor.Parameters.Count < forcedParams.Position) {
        log.LogError($"No parameter with index {forcedParams.Position} found in controller action {ctx.ActionDescriptor.DisplayName}");
        return;
      }

      var paramDesc= ctx.ActionDescriptor.Parameters[forcedParams.Position];

      foreach (var name in forcedParams.Values.Keys) {
        var value= forcedParams.Values[name];
        var param= ctx.ActionArguments[paramDesc.Name];

        if (param != null && param.GetType().IsGenericType && param.GetType().GetGenericTypeDefinition() == typeof(FilterParam<>)) {
          dynamic filter= ctx.ActionArguments[paramDesc.Name];
          List<Filter> enforcedFilters= new List<Filter>(filter.FilterList);

          var prop= enforcedFilters.FirstOrDefault(x => x.property == name);

          if (null != prop) {
            // If user is filtering by this property with a different value, show him nothing
            prop.value= value.StartsWith(prop.value, StringComparison.OrdinalIgnoreCase) ? value : "#########";
          }
          else {
            enforcedFilters.Add(new Filter { property= name, value= value });
          }
          filter.FilterList= enforcedFilters;
        }
      }
    }

    private Role.EnforcedParameter paramsForRole(ActionExecutingContext ctx) {
      var idSrvc= (Tlabs.Identity.IIdentityAccessor)App.ServiceProv.GetService(typeof(Tlabs.Identity.IIdentityAccessor));
      if (null == idSrvc.Name) return null;

      var currentRoles= idSrvc.Roles;
      var roles= currentRoles.Where(x => null != x).Select(x => Role.Cache[x, loadRole(x)]);

      var role= roles.FirstOrDefault(r => r.AllowAccessPattern != null);

      if (role == null) return null;
      return role.ParamsForAction(ctx.ActionDescriptor.AttributeRouteInfo.Template.ToLower());
    }

    private Role loadRole(string name) {
      Role role= null;
      App.WithServiceScope(prov => {
        var repo= (IRepo<Role>)prov.GetService(typeof(IRepo<Role>));
        role= repo.AllUntracked.Single(x => x.Name == name);
      });
      return role;
    }

    /// <summary>Configurator</summary>
    public class Configurator : IConfigurator<IServiceCollection> {
      /// <inheritoc/>
      public void AddTo(IServiceCollection svcColl, IConfiguration cfg) {
        svcColl.AddSingleton(new RoleDefaultParamsFilter());
      }
    }
  }
}