using System;
using System.Collections.Generic;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Http;

namespace Tlabs.Config {

  ///<summary>Configures Identity Framework.</summary>
  public class KeycloakIdentityConfigurator : IConfigurator<IServiceCollection> {
    readonly IDictionary<string, string> config;

    ///<summary>Default ctor.</summary>
    public KeycloakIdentityConfigurator() : this(null) { }

    ///<summary>Ctor from <paramref name="config"/>.</summary>
    public KeycloakIdentityConfigurator(IDictionary<string, string>? config) {
      this.config = config ?? new Dictionary<string, string>();
    }

    ///<inheritdoc/>
    public void AddTo(IServiceCollection services, IConfiguration cfg) {
      var log = App.Logger<KeycloakIdentityConfigurator>();

      var authority = cfg["config:client:Authority"] ?? throw new ArgumentNullException("Client Authority not configured");
      var audience = cfg["config:client:Audience"] ?? throw new ArgumentNullException("Client Audience not configured");
      var requireHttpsMetadata = bool.TryParse(cfg["config:client:RequireHttpsMetadata"], out var reqHttps) ? reqHttps : throw new ArgumentNullException("Client RequireHttpsMetadata not configured");

      services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options => {
          options.Authority = authority;
          options.Audience = audience;
          options.RequireHttpsMetadata = requireHttpsMetadata;

          options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromMinutes(2)
          };
        });

      services.AddHttpClient();

      log.LogInformation("AspNetCore.Identity services added");
      services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>(); //typically AddIdentity() already registers the accessor
    }
  }
}
