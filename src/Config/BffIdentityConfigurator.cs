using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Security.Claims;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

using Tlabs.Identity;
using Tlabs.Config;
using Microsoft.AspNetCore.Authentication;
using Tlabs.Server.Auth.Keycloak;

namespace Tlabs.Server.Config {

  ///<summary>Configures IdentityFramework.</summary>
  public class BffIdentityConfigurator : IConfigurator<IServiceCollection> {
    readonly IDictionary<string, string> config;

    ///<summary>Default ctor.</summary>
    public BffIdentityConfigurator() : this(null) { }

    ///<summary>Ctor from <paramref name="config"/>.</summary>
    public BffIdentityConfigurator(IDictionary<string, string>? config) {
      this.config = config ?? new Dictionary<string, string>();
    }

    ///<inheritdoc/>
    public void AddTo(IServiceCollection services, IConfiguration cfg) {
      var log = App.Logger<BffIdentityConfigurator>();
      services.AddOptions<KeycloakConfig>().Bind(cfg.GetSection("config")).ValidateDataAnnotations();

      var keycloakConfig = cfg.GetSection("config").Get<KeycloakConfig>() ?? throw new ArgumentException(nameof(KeycloakConfig));

      if(keycloakConfig.Client?.BaseUrl == null || keycloakConfig.Client.Realm == null) throw new ArgumentNullException("Client BaseUrl or Realm not configured");
      var authority = keycloakConfig.Client.Authority;
      var clientId = keycloakConfig.Client?.ClientId ?? throw new ArgumentNullException("Client ClientId not configured");
      var clientSecret = keycloakConfig.Client?.ClientSecret ?? throw new ArgumentNullException("Client ClientSecret not configured");
      var logoutRedirect = keycloakConfig.Client?.LogoutRedirect ?? throw new ArgumentNullException("Client LogoutRedirect not configured");
      var requireHttpsMetadata = keycloakConfig.Client?.RequireHttpsMetadata ?? throw new ArgumentNullException("Client RequireHttpsMetadata not configured");

      // OIDC
      services.AddAuthentication(options => {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
      })
      .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options => {
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.SlidingExpiration = false;
      })
      .AddOpenIdConnect(options => {
        options.Authority = authority;
        options.RequireHttpsMetadata = requireHttpsMetadata;
        options.ClientId = clientId;
        options.ClientSecret = clientSecret;
        options.ResponseType = OpenIdConnectResponseType.Code;
        options.SaveTokens = true;

        options.GetClaimsFromUserInfoEndpoint = true;
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters {
          NameClaimType = "preferred_username",
          RoleClaimType = ClaimTypes.Role
        };
        options.Events = new OpenIdConnectEvents {
          OnRedirectToIdentityProviderForSignOut = async ctx => {
            var logoutUri = $"{authority}/protocol/openid-connect/logout?post_logout_redirect_uri={Uri.EscapeDataString(logoutRedirect)}";
            var idToken = await ctx.HttpContext.GetTokenAsync("id_token");

            if (!string.IsNullOrWhiteSpace(idToken)) {
              logoutUri += $"&id_token_hint={idToken}";
            }
            ctx.Response.Redirect(logoutUri);
            ctx.HandleResponse();
          },
          OnTokenValidated = context => {
            var identity = (ClaimsIdentity)context.Principal!.Identity!;

            // Extract roles from realm_access.roles
            var realmAccess = context.Principal!.FindFirst("realm_access")?.Value;
            if (!string.IsNullOrEmpty(realmAccess)) {
              identity.AddClaim(new Claim(identity.RoleClaimType, realmAccess));
            }

            return Task.CompletedTask;
          }
        };
      }
      );

      services.AddAuthorization();

      services.AddHttpClient();

      log.LogInformation("AspNetCore.Identity services added");

      services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>(); //typically AddIdentity() already registers the accessor
      services.AddSingleton<IIdentityAccessor, HttpContextIdentityAccessor>();
      services.AddScoped<IClaimsTransformation, KeycloakRolesClaimsTransformation>();
    }
  }
}
