using System;
using System.Collections.Generic;
using System.Net;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Http;

using Tlabs.Data.Entity;
using Tlabs.Identity;
using Tlabs.Identity.Intern;

namespace Tlabs.Config {

  ///<summary>Configures IdentiytFramework.</summary>
  public class IdentityConfigurator : IConfigurator<IServiceCollection> {
    IDictionary<string, string> config;

    ///<summary>Default ctor.</summary>
    public IdentityConfigurator() : this(null) { }

    ///<summary>Ctor from <paramref name="config"/>.</summary>
    public IdentityConfigurator(IDictionary<string, string> config) {
      this.config= config ?? new Dictionary<string, string>();
    }

    ///<inherit/>
    public void AddTo(IServiceCollection services, IConfiguration cfg) {
      var log= App.Logger<IdentityConfigurator>();

      services.AddIdentity<User, Role>()
              .AddDefaultTokenProviders();

      services.Configure<IdentityOptions>(options => {
        // Password settings
        var pwOptions= new PasswordOptions();
        string cfgStr;
        config.TryGetValue(nameof(pwOptions.RequireDigit), out cfgStr);
        pwOptions.RequireDigit= Boolean.Parse(cfgStr ?? "true");

        config.TryGetValue(nameof(pwOptions.RequiredLength), out cfgStr);
        pwOptions.RequiredLength= int.Parse(cfgStr ?? "8");

        config.TryGetValue(nameof(pwOptions.RequireLowercase), out cfgStr);
        pwOptions.RequireLowercase= Boolean.Parse(cfgStr ?? "true");

        config.TryGetValue(nameof(pwOptions.RequireNonAlphanumeric), out cfgStr);
        pwOptions.RequireNonAlphanumeric= Boolean.Parse(cfgStr ?? "false");

        config.TryGetValue(nameof(pwOptions.RequireUppercase), out cfgStr);
        pwOptions.RequireUppercase= Boolean.Parse(cfgStr ?? "false");

        options.Password= pwOptions;

        // User settings
        config.TryGetValue(nameof(options.User.RequireUniqueEmail), out cfgStr);
        options.User.RequireUniqueEmail= Boolean.Parse(cfgStr ?? "false");

        // Lockout settings
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(30);
        options.Lockout.MaxFailedAccessAttempts = 10;
      });

      services.ConfigureApplicationCookie(options => {
        string cfgStr;
        if (config.TryGetValue("idleLogoffMinutes", out cfgStr) && cfgStr != null) {
          int minutes;
          Int32.TryParse(cfgStr, out minutes);
          if (minutes > 0) {
            options.SlidingExpiration= true;
            options.ExpireTimeSpan= new TimeSpan(0, minutes, 0);
          }
        }
        options.Events= new CookieAuthenticationEvents {
          OnRedirectToAccessDenied= ctx => {
            if (ctx.Request.Path.StartsWithSegments("/api") && ctx.Response.StatusCode == (int)HttpStatusCode.OK) {
              ctx.Response.StatusCode = (int)HttpStatusCode.Forbidden;
              ctx.Response.ContentType = "application/json";
            }
            else {
              ctx.Response.Redirect(ctx.RedirectUri);
            }
            return System.Threading.Tasks.Task.FromResult(0);
          },
          OnRedirectToLogin= ctx => {
            if (ctx.Request.Path.StartsWithSegments("/api") && ctx.Response.StatusCode == (int)HttpStatusCode.OK) {
              ctx.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
              ctx.Response.ContentType = "application/json";
            }
            else {
              ctx.Response.Redirect(ctx.RedirectUri);
            }
            return System.Threading.Tasks.Task.FromResult(0);
          }
        };
      });

      services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie();

      log.LogInformation("AspNetCore.Identity services added");

      services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>(); //typically AddIdentity() already registers the accessor
      services.AddSingleton<IIdentityAccessor, HttpContextIdentityAccessor>();
      services.AddScoped<IUserStore<User>, UserIdentityStore>();
      services.AddScoped<IRoleStore<Role>, UserRoleStore>();
      services.AddScoped<IUserAdministration, UserAdministration>();
      services.AddSingleton<IRolesAdministration, SingletonRolesAdministration>();
    }
  }
}
