using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Newtonsoft.Json;

using Tlabs.Data.Serialize.Json;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Tlabs.Data.Entity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Net;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Http;
using Tlabs.Identity;
using Tlabs.Server.Identity;
using System.Reflection;

namespace Tlabs.Config {

  ///<summary>Configures MVC to <see cref="IServiceCollection"/>>.</summary>
  public class MvcSvcConfigurator : IConfigurator<IServiceCollection> {
    IDictionary<string, string> config;

    ///<summary>Default ctor.</summary>
    public MvcSvcConfigurator() : this(null) { }

    ///<summary>Ctor from <paramref name="config"/>.</summary>
    public MvcSvcConfigurator(IDictionary<string, string> config) {
      this.config= config ?? new Dictionary<string, string>();
    }

    ///<inherit/>
    public void AddTo(IServiceCollection services, IConfiguration cfg) {
      var log= App.Logger<MvcSvcConfigurator>();

      // Add ASP.NET MVC framework services.
      string authActiveStr;
      bool authActive;
      config.TryGetValue("authentication", out authActiveStr);
      Boolean.TryParse(authActiveStr, out authActive);
      services.AddMvc(config => {
        if (authActive) {
          var policy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
          config.Filters.Add(new BriefTemplAuthorizationFilter(policy));
          // config.Filters.Add(new ProtectedInsureesFilter());
        }
      }).AddJsonOptions(configureJsonOptions)
        .AddApplicationPart(Assembly.GetEntryAssembly());

      log.LogInformation("ASP.NET MVC framework services added.");
    }

    private void configureJsonOptions(MvcJsonOptions jsonOpt) {
      /* Use same settings as with JsonFormat.Settings:
       * (Unfortunately neither MvcJsonOptions.SerializerSettings could be set nor a CopyTo exists...)
       */
      jsonOpt.SerializerSettings.NullValueHandling= JsonFormat.NewtonJsonSingleton.Settings.NullValueHandling;
      jsonOpt.SerializerSettings.Converters= JsonFormat.NewtonJsonSingleton.Settings.Converters;
      jsonOpt.SerializerSettings.DateFormatHandling= JsonFormat.NewtonJsonSingleton.Settings.DateFormatHandling;
      jsonOpt.SerializerSettings.ContractResolver= JsonFormat.NewtonJsonSingleton.Settings.ContractResolver;
      // var cr= new DefaultContractResolver();
      // cr.NamingStrategy= new CamelCaseNamingStrategy();
      // jsonOpt.SerializerSettings.ContractResolver= cr;

      string frmt;
      if (config.TryGetValue("formatting", out frmt))
        jsonOpt.SerializerSettings.Formatting= (Formatting)Enum.Parse(typeof(Formatting), frmt, true);
    }
  }

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
        config.TryGetValue("RequireDigit", out cfgStr);
        pwOptions.RequireDigit= Boolean.Parse(cfgStr?? "true");

        config.TryGetValue("RequiredLength", out cfgStr);
        pwOptions.RequiredLength= int.Parse(cfgStr?? "8");

        config.TryGetValue("RequireLowercase", out cfgStr);
        pwOptions.RequireLowercase= Boolean.Parse(cfgStr?? "true");

        config.TryGetValue("RequireNonAlphanumeric", out cfgStr);
        pwOptions.RequireNonAlphanumeric= Boolean.Parse(cfgStr?? "false");

        config.TryGetValue("RequireUppercase", out cfgStr);
        pwOptions.RequireUppercase= Boolean.Parse(cfgStr?? "false");

        options.Password= pwOptions;

        // User settings
        config.TryGetValue("RequireUniqueEmail", out cfgStr);
        options.User.RequireUniqueEmail = Boolean.Parse(cfgStr?? "false");

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

      services.AddAuthentication(options => {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
      });

      log.LogInformation("AspNetCore.Identity services added");

      services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>(); //typically AddIdentity() already registers the accessor
      services.AddSingleton<IIdentityAccessor, HttpContextIdentityAccessor>();
      services.AddScoped<IUserStore<User>, UserIdentityStore>();
      services.AddScoped<IRoleStore<Role>, UserRoleStore>();
    }
  }
}