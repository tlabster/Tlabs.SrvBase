using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Filters;

using Tlabs;
using Tlabs.Config;
using Tlabs.Server.Model;
using System.Net.Http;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc.Authorization;
using Tlabs.Data.Serialize.Json;
using System.Threading.Tasks;

namespace Tlabs.Server.Auth.Opa {
  ///<summary>Filter that applies default action parameters based on the authenticated user</summary>
  public class OpaDefaultParamsFilter : IActionFilter {
    private static readonly ILogger log = Tlabs.App.Logger<OpaDefaultParamsFilter>();

    ///<summary>Ctor</summary>
    public OpaDefaultParamsFilter() { }

    /// <inheritdoc/>
    public void OnActionExecuting(ActionExecutingContext context) {
      ApplyFilterParams(context);
    }

    /// <inheritdoc/>
    public void OnActionExecuted(ActionExecutedContext context) {
      return;
    }

    private static void ApplyFilterParams(ActionExecutingContext ctx) {
      if (ctx.HttpContext.IsAnonymous()) return;
      if (ctx.HttpContext.Items.TryGetValue("OpaEnforcedFilter", out var filter) && filter is string enforcedFilter) {
        if (enforcedFilter != null && enforcedFilter.Contains('>')) {
          var parts = enforcedFilter.Split(">");
          if (parts.Length < 2) {
            log.LogWarning("Wrongly formatted constraints");
            return;
          }

          int position = 0;
          var success = int.TryParse(parts[0], out position);

          if (!success) {
            log.LogWarning("Wrongly formatted constraints");
            return;
          }

          var paramDesc = ctx.ActionDescriptor.Parameters[position];
          var forcedParams = new Dictionary<string, string>();
          foreach (var g in parts[1].Split('#')) {
            if (0 == g.Length) continue;
            var kv= g.Split("=");
            forcedParams.Add(kv[0], kv[1]);
          }

          foreach (var name in forcedParams.Keys) {
            var value = forcedParams[name];
            var param = ctx.ActionArguments[paramDesc.Name];

            if (param != null && param.GetType().IsGenericType && param.GetType().GetGenericTypeDefinition() == typeof(FilterParam<>)) {
              var filterListProperty = param.GetType().GetProperty("FilterList");

              var filterListValue = filterListProperty?.GetValue(param) as List<Filter>;
              List<Filter> enforcedFilters = [.. filterListValue ?? []];

              var prop = enforcedFilters.FirstOrDefault(x => x.property == name);

              if (null != prop && null != prop.value) {
                // If user is filtering by this property with a different value, show him nothing
                prop.value = value.StartsWith(prop.value, StringComparison.OrdinalIgnoreCase) ? value : "#########";
              }
              else {
                enforcedFilters.Add(new Filter { property = name, value = value });
              }
              filterListProperty?.SetValue(param, enforcedFilters);
            }
          }
        }
      }
    }
  }
}