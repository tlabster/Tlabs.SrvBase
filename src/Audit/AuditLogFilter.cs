using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tlabs.Config;
using System;
using System.Threading.Tasks;
using System.Linq;

namespace Tlabs.Server.Audit {
  ///<summary>Action filter to store audit trail.</summary>
  public class AuditLogFilter : IAsyncResultFilter, IExceptionFilter {
    ///<summary>Default Ctor.</summary>
    public AuditLogFilter() { }

    ///<inheritdoc/>
    public void OnException(ExceptionContext context) {
      // Store trail even in case an unhandled exception would happen
      App.WithServiceScope(scope => {
        var trail= scope.GetService<IAuditTrail>();
        trail.StoreTrail(context, true);
      });
    }

    ///<inheritdoc/>
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next) {
      // Code here would happen before the action
      await next();

      // Code here would happen after the action
      App.WithServiceScope(scope => {
        var trail= scope.GetService<IAuditTrail>();
        var storeBody= context.Filters.Any(item => item is AuditRequestBodyAttribute);

        trail.StoreTrail(context, storeBody);
      });
    }

    /// <summary>Configurator</summary>
    public class Configurator : IConfigurator<IServiceCollection> {
      /// <inheritoc/>
      public void AddTo(IServiceCollection svcColl, IConfiguration cfg) {
        svcColl.AddSingleton<AuditLogFilter>();
        svcColl.AddScoped<IAuditTrail, AuditTrail>();
      }
    }
  }

  ///<summary>Request body must be included in audit</summary>
  [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
  public class AuditRequestBodyAttribute : Attribute {
    ///<summary>Default ctor.</summary>
    public AuditRequestBodyAttribute() {}
  }
}