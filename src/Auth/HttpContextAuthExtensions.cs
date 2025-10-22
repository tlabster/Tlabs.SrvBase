using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Tlabs.Server.Auth {
  /// <summary>
  /// Auth extensions for HttpContext
  /// </summary>
  public static class HttpContextAuthExtensions {
    /// <summary>
    /// Checks whether the current request endpoint allows anonymous access.
    /// </summary>
    public static bool IsAnonymous(this HttpContext context) {
      /* When doing endpoint routing, MVC does not add AllowAnonymousFilters for AllowAnonymousAttributes that
        * were discovered on controllers and actions.
        * As a workaround we check for the presence of IAllowAnonymous in endpoint metadata.
        * (https://docs.microsoft.com/en-us/dotnet/core/compatibility/aspnetcore#authorization-iallowanonymous-removed-from-authorizationfiltercontextfilters)
        * Skip filter if header is marked as anonymous or apiKey was provided and filter did not short circuit the pipeline
        */
      var endpoint = context.GetEndpoint();
      return endpoint?.Metadata?.GetMetadata<IAllowAnonymous>() is not null;
    }
  }
}
