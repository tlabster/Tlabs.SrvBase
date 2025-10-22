using System;

namespace Tlabs.Server.Exceptions {
  /// <summary>
  /// Exception thrown when an operation requiring a logged-in user is attempted without authentication.
  /// </summary>
  public class AuthenticationRequiredException : GeneralException {
    const string TMPL_MSG= "No user logged in";

    /// <summary>
    /// Default Ctor
    /// </summary>
    public AuthenticationRequiredException()
        : base(ExceptionDataKey.ResolvedMsgParams(TMPL_MSG, out var tmpData)) {
      this.SetMsgData(tmpData);
    }

    /// <summary>
    /// Ctor from inner <paramref name="innerException"/>.
    /// </summary>
    /// 
    public AuthenticationRequiredException(Exception innerException)
        : base(ExceptionDataKey.ResolvedMsgParams(TMPL_MSG, out var tmpData), innerException) { }
  }
}
