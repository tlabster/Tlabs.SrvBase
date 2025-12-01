using System;
using System.Collections.Generic;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication;
using Tlabs.Server.Auth.Keycloak;

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

      var settings = cfg.GetSection("config:client").Get<KeycloakIdentityOptions>() ?? throw new ArgumentNullException("Could not map the Keycloak identity options");
      ArgumentException.ThrowIfNullOrEmpty(settings.Authority);
      ArgumentException.ThrowIfNullOrEmpty(settings.Audience);

      services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options => {
          options.Authority = settings.Authority;
          options.Audience = settings.Audience;
          options.RequireHttpsMetadata = settings.RequireHttpsMetadata;

          options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            NameClaimType = "preferred_username",
            ValidIssuer = settings.ValidIssuer,
            ValidAudience = settings.ValidateAudiences == null || settings.ValidateAudiences.Count == 0 ? settings.Audience : null,
            ValidAudiences = settings.ValidateAudiences,
            ClockSkew = TimeSpan.FromMinutes(2)
          };
        });

      services.AddHttpClient();

      services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>(); //typically AddIdentity() already registers the accessor
      services.AddScoped<IClaimsTransformation, KeycloakRolesClaimsTransformation>();
      log.LogInformation("AspNetCore.Identity services added");
    }
  }

  internal class KeycloakIdentityOptions {
    public string? Authority { get; set; }
    public string? Audience { get; set; }
    public string? ValidIssuer { get; set; }
    public bool RequireHttpsMetadata { get; set; } = false;
    public List<string> ValidateAudiences { get; set; } = [];
  }
}
