using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Tlabs.Server.Auth {
  /// <summary>
  /// Extensions for AuthorizationFilterContext
  /// </summary>
  public static class AuthorizationFilterContextExtensions {
    /// <summary>
    /// Checks whether the current request endpoint allows anonymous access.
    /// </summary>
    public static bool IsAnonymous(this AuthorizationFilterContext context) {
      /* When doing endpoint routing, MVC does not add AllowAnonymousFilters for AllowAnonymousAttributes that
        * were discovered on controllers and actions.
        * As a workaround we check for the presence of IAllowAnonymous in endpoint metadata.
        * (https://docs.microsoft.com/en-us/dotnet/core/compatibility/aspnetcore#authorization-iallowanonymous-removed-from-authorizationfiltercontextfilters)
        * Skip filter if header is marked as anonymous or apiKey was provided and filter did not short circuit the pipeline
        */
      var endpoint = context.HttpContext.GetEndpoint();
      return endpoint?.Metadata?.GetMetadata<IAllowAnonymous>() is not null;
    }
  }
}
