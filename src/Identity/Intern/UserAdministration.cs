using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

using Tlabs.Misc;
using Tlabs.Data;
using Tlabs.Data.Entity;
using Tlabs.Data.Model;

namespace Tlabs.Identity.Intern {

  ///<summary>User credentitals service.</summary>
  public sealed class UserAdministration : IUserAdministration {
    ///<summary>User credentitals options.</summary>
    static readonly BasicCache<string, FailedLogin> failedLogins= new BasicCache<string, FailedLogin>();
    readonly HttpContext httpCtx;
    readonly UserManager<Data.Entity.User> userManager;
    readonly SignInManager<Data.Entity.User> signInManager;
    readonly ILookupNormalizer norm;
    readonly ICachedRepo<Tlabs.Data.Entity.Locale> locRepo;
    readonly IDataStore store;
    readonly IdentityOptions identOptions;

    static readonly ILogger<UserAdministration> log= Tlabs.App.Logger<UserAdministration>();
    static readonly IDictionary<string, QueryFilter.FilterExpression<Data.Entity.User>> userFilterMap= new Dictionary<string, QueryFilter.FilterExpression<Data.Entity.User>>(StringComparer.OrdinalIgnoreCase) {
      [nameof(Data.Entity.User.UserName)]= (q, cv) => q.Where(u => u.UserName.StartsWith(cv.ToString())),
      [nameof(Data.Entity.User.Email)]= (q, cv) => q.Where(u => u.Email.StartsWith(cv.ToString())),
      [nameof(Data.Entity.User.Firstname)]= (q, cv) => q.Where(u => u.Firstname.StartsWith(cv.ToString())),
      [nameof(Data.Entity.User.Lastname)]= (q, cv) => q.Where(u => u.Lastname.StartsWith(cv.ToString())),
      [nameof(Data.Entity.User.Status)]= (q, cv) => q.Where(u => u.Status.StartsWith(cv.ToString()))
    };
    static readonly IDictionary<string, QueryFilter.SorterExpression<Data.Entity.User>> userSorterMap= new Dictionary<string, QueryFilter.SorterExpression<Data.Entity.User>>(StringComparer.OrdinalIgnoreCase) {
      [nameof(Data.Entity.User.UserName)]= (q, isAsc) => isAsc ? q.OrderBy(u => u.UserName) : q.OrderByDescending(u => u.UserName),
      [nameof(Data.Entity.User.Email)]= (q, isAsc) => isAsc ? q.OrderBy(u => u.Email) : q.OrderByDescending(u => u.Email),
      [nameof(Data.Entity.User.Firstname)]= (q, isAsc) => isAsc ? q.OrderBy(u => u.Firstname) : q.OrderByDescending(u => u.Firstname),
      [nameof(Data.Entity.User.Lastname)]= (q, isAsc) => isAsc ? q.OrderBy(u => u.Lastname) : q.OrderByDescending(u => u.Lastname),
      [nameof(Data.Entity.User.Status)]= (q, isAsc) => isAsc ? q.OrderBy(u => u.Status) : q.OrderByDescending(u => u.Status)
    };

    ///<summary>Ctor from .</summary>
    public UserAdministration(IHttpContextAccessor httpCtxAcc,
                              UserManager<Data.Entity.User> userManager,
                              SignInManager<Data.Entity.User> signInManager,
                              ICachedRepo<Tlabs.Data.Entity.Locale> locRepo,
                              IOptions<IdentityOptions> identOpt) {
      if (null == (this.httpCtx= httpCtxAcc?.HttpContext)) throw new ArgumentNullException(nameof(httpCtxAcc));
      if (null == (this.userManager= userManager)) throw new ArgumentNullException(nameof(userManager));
      if (null == (this.signInManager= signInManager)) throw new ArgumentNullException(nameof(signInManager));
      this.norm= userManager.KeyNormalizer ?? new DefaultNormalizer();
      if (null == (this.locRepo= locRepo)) throw new ArgumentNullException(nameof(locRepo));
      this.store= locRepo.Store;
      if (null == (this.identOptions= identOpt?.Value)) throw new ArgumentNullException(nameof(identOpt));
    }

    private IQueryable<Data.Entity.User> loadUser(IQueryable<Data.Entity.User> query)
      => query.LoadRelated(store, u => u.Locale)
              .LoadRelated(store, u => u.Roles)
              .ThenLoadRelated(store, r => r.Role);

    ///<inheritdoc/>
    public IResultList<Data.Model.User> FilteredList(QueryFilter filter= null) {
      filter??= new QueryFilter();

      var query= loadUser(filter.Apply(userManager.Users, userFilterMap, userSorterMap));
      return new QueryResult<Data.Entity.User, Data.Model.User>(query, filter, u => new Data.Model.User(u));
    }

    ///<inheritdoc/>
    public Data.Model.User GetByName(string userName)
      => new Data.Model.User(getByName(userName));

    Data.Entity.User getByName(string userName)
      => loadUser(userManager.Users).SingleEntity(u => u.NormalizedUserName == norm.NormalizeName(userName), userName);

    Data.Entity.User tryGetByName(string userName)
      => loadUser(userManager.Users).SingleOrDefault(u => u.NormalizedUserName == norm.NormalizeName(userName)); //entity or null

    ///<inheritdoc/>
    public Data.Model.User GetByEmail(string email)
      => new Data.Model.User(loadUser(userManager.Users.Where(u => u.NormalizedEmail == norm.NormalizeEmail(email))).SingleEntity(email));

    ///<inheritdoc/>
    public Data.Model.User GetLoggedIn(ClaimsPrincipal principal) {
      /* Any currently logged-in user has its name set with the principal's Identity:
       */
      Data.Entity.User usr= null;
      var userName= principal?.Identity?.Name;
      if (! string.IsNullOrEmpty(userName) && null != (usr= tryGetByName(userName)))
        return new Data.Model.User(usr);      //return user with role information

#if DEBUG
      var entUsr= new Data.Entity.User { UserName= "NoAuth" };
      entUsr.Roles= new List<Data.Entity.User.RoleRef> { new Data.Entity.User.RoleRef { User= entUsr, Role= new Data.Entity.Role { Name= "NoAuthAdmin", AllowAccessPattern= "[A-Z]+:.*" } } }; //"NoAuth" is super user
      return new Data.Model.User(entUsr);
#endif

      throw new InvalidOperationException("No user session.");
    }

    ///<inheritdoc/>
    public void Create(Data.Model.User user) {
      var entUsr= user.AsEntity(locRepo);
      assignRoles(entUsr, user.RoleIds);
      throwOnIdentiyError(
        userManager.CreateAsync(entUsr, user.Password).GetAwaiter().GetResult(),
        "Failed to create user '{name}'", entUsr.UserName
      );
      log.LogInformation("New user account created for {usr}", entUsr.UserName);
    }

    ///<inheritdoc/>
    public void Update(Data.Model.User user) {
      var entUsr= user.CopyTo(getByName(user.Username), locRepo);   //copy/merge to entity
      if (! string.IsNullOrEmpty(user.Password)) {
        var token= userManager.GeneratePasswordResetTokenAsync(entUsr).GetAwaiter().GetResult();
        throwOnIdentiyError(
          userManager.ResetPasswordAsync(entUsr, token, user.Password).GetAwaiter().GetResult(),
          "Failed to update password for user '{name}'", entUsr.UserName
        );
        log.LogInformation("New passwort set for user account: {usr}", entUsr.UserName);
      }
      assignRoles(entUsr, user.RoleIds);
      throwOnIdentiyError(
        userManager.UpdateAsync(entUsr).GetAwaiter().GetResult(),
        "Failed to update user '{name}'", entUsr.UserName
      );
      failedLogins.Evict(entUsr.UserName);
      log.LogInformation("Updated user account: {usr}", entUsr.UserName);
    }

    void assignRoles(Data.Entity.User usrEnt, IEnumerable<string> usrRoles) {
      if(usrRoles == null)
        return;

      var existingRefs= store.Query<Data.Entity.User.RoleRef>()
                             .Where(@ref => @ref.User.UserName == usrEnt.UserName);
      var currentNames= existingRefs.Select(x => x.Role.Name)
                                    .ToList();

      var toInsert= usrRoles.Where(r => !currentNames.Contains(r));
      var newRoles= store.Query<Data.Entity.Role>()
                         .Where(r => toInsert.Contains(r.Name));
      store.Attach<Data.Entity.User>(usrEnt);
      foreach (var role in newRoles) {
        store.Insert<Data.Entity.User.RoleRef>(new Data.Entity.User.RoleRef {
          User= usrEnt,
          Role= role
        });
      }

      var toRemove= currentNames.Where(n => !usrRoles.Contains(n));
      foreach (var roleRef in existingRefs.Where(x => toRemove.Contains(x.Role.Name))) {
        store.Delete<Data.Entity.User.RoleRef>(roleRef);
      }
    }

    ///<inheritdoc/>
    public void Delete(string userName) {
      var entUsr= userManager.FindByNameAsync(userName).GetAwaiter().GetResult();
      throwOnIdentiyError(
        userManager.DeleteAsync(entUsr).GetAwaiter().GetResult(),
        "Failed to delete user '{name}'", entUsr.UserName
      );
      log.LogInformation("Deleted user account: {usr}", entUsr.UserName);
    }

    ///<inheritdoc/>
    public async Task<LoginResult> Login(string userName, string pwd) {
      LoginResult res;

      if (string.IsNullOrEmpty(pwd)) throw new ArgumentNullException(nameof(pwd));
      if (LoginResult.SUCCESS != (res= userCanLogin(userName, out var user))) return res;


      if (! (await signInManager.CheckPasswordSignInAsync(user, pwd, false)).Succeeded) {
        var failed= failedLogins[userName, () => new FailedLogin()].Increment();
        log.LogInformation("{cnt} consecutive failed login(s) for user {usr}", failed.Count, userName);
        //TODO: Raise failed login event
        return user.Status == Data.Entity.User.State.DEACTIVATED.ToString() ? LoginResult.DEACTIVATED : LoginResult.FAILED;
      }

      failedLogins.Evict(userName);

#if false
//*** TODO: check second factor required?
        /* Set first factor user identity cookie:
         */
        var secFactorChallengeId= "?";
        await httpCtx.SignInAsync(IdentityConstants.TwoFactorUserIdScheme, createPreliminaryIdentity(user, secFactorChallengeId));
        return LoginResult.SECOND_FACTOR_MISSING;
      }
#endif
      /* Set user login identity cookie:
       */
      await httpCtx.SignInAsync(IdentityConstants.ApplicationScheme, createLoginIdentity(user));
      return LoginResult.SUCCESS;
    }

    private LoginResult userCanLogin(string userName, out Data.Entity.User user) {
      user= null;
      if (string.IsNullOrEmpty(userName)) throw new ArgumentNullException(nameof(userName));
      if (true == failedLogins[userName]?.IsLockedOut()) return LoginResult.LOCKED_OFF;

      user= tryGetByName(userName);

      if (null == user) {
        log.LogInformation("Login for unknown user {usr} failed.", userName);
        return LoginResult.FAILED;
      }

      if (user.Status != Data.Entity.User.State.ACTIVE.ToString()) {
        log.LogInformation("Login for user {usr} with status {state} failed.", user?.UserName, user?.Status);
        return LoginResult.DEACTIVATED;
      }

      return LoginResult.SUCCESS;
    }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0051:Remove unused private member", Justification = "Might be needed in future")]
    static ClaimsPrincipal createPreliminaryIdentity(Data.Entity.User user, string msgId) {
      var ident= new ClaimsIdentity(IdentityConstants.TwoFactorUserIdScheme);
      ident.AddClaim(new Claim(ClaimTypes.Name, user.UserName));
      ident.AddClaim(new Claim(ClaimTypes.Sid, msgId));

      return new ClaimsPrincipal(ident);
    }

    private ClaimsPrincipal createLoginIdentity(Data.Entity.User user) {
      var userId= userManager.GetUserIdAsync(user).GetAwaiter().GetResult();
      var userName= userManager.GetUserNameAsync(user).GetAwaiter().GetResult();
      var roles= userManager.GetRolesAsync(user).GetAwaiter().GetResult();
      var ident= new ClaimsIdentity(IdentityConstants.ApplicationScheme,
                                    identOptions.ClaimsIdentity.UserNameClaimType,
                                    identOptions.ClaimsIdentity.RoleClaimType);
      ident.AddClaim(new Claim(identOptions.ClaimsIdentity.UserIdClaimType, userId));
      ident.AddClaim(new Claim(identOptions.ClaimsIdentity.UserNameClaimType, userName));
      ident.AddClaims(roles.Select(r => new Claim(identOptions.ClaimsIdentity.RoleClaimType, r)));

      return new ClaimsPrincipal(ident);
    }

    ///<inheritdoc/>
    public async Task<LoginResult> SecondFactorLogin(string userName, string token) {
      LoginResult res;

      if (string.IsNullOrEmpty(token)) throw new ArgumentNullException(nameof(token));
      if (LoginResult.SUCCESS != (res= userCanLogin(userName, out var user))) return res;

      if (! await isValidSecondFactor(user, token)) return LoginResult.FAILED;

      /* Set user login identity cookie:
       */
      await httpCtx.SignInAsync(IdentityConstants.ApplicationScheme, createLoginIdentity(user));
      return LoginResult.SUCCESS;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unread parameter", Justification = "token needed for future sec. factor check.")]
    private async Task<bool> isValidSecondFactor(Data.Entity.User user, string token) {
      var firstFactorIdent= (await httpCtx.AuthenticateAsync(IdentityConstants.TwoFactorUserIdScheme)).Principal;

      /* Cleanup first factor user identity cookie:
       */
      await httpCtx.SignOutAsync(IdentityConstants.TwoFactorUserIdScheme);

      if (user.UserName != firstFactorIdent?.FindFirstValue(ClaimTypes.Name)) {
        log.LogInformation("Second factor login for user {usr} failed.", user.UserName);
        return false;
      }

      // var msgId= firstFactorIdent?.FindFirstValue(ClaimTypes.Sid);
      bool isValid;
      try {
        isValid= false; //*** TODO: Check validity of sec. factor token with msgId
      }
      catch (Exception e) {
        log.LogInformation("Second factor token lookup for user {usr} failed ({msg}).", user.UserName, e.Message);
        return false;
      }

      if (false == isValid)
        log.LogInformation("Second factor token verification for user {usr} failed.", user.UserName);

      return isValid;
    }

    ///<inheritdoc/>
    public void LogoffCurrent() {
      var currUser= httpCtx.User;
      if (null == currUser) return;
      var usrName= currUser.Identity.Name;
      httpCtx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme)
             .GetAwaiter().GetResult();
      signInManager.SignOutAsync()
                   .GetAwaiter().GetResult(); //signout with Identity schemes
      if (null != usrName) failedLogins.Evict(usrName);
      log.LogInformation("Logged off user {usr}", usrName);
    }

    static void throwOnIdentiyError(IdentityResult res, string msg, params object[] args) {
      if (res.Succeeded) return;
      var ex= Tlabs.EX.New<ArgumentException>(msg, args);
      ex.Data[(ExceptionDataKey)"IdentityErrors"]= res.Errors;
      throw ex;
    }

    ///<inheritdoc/>
    public bool NomalizedNameEquals(string k1, string k2) => string.Equals(norm.NormalizeName(k1), norm.NormalizeName(k2), StringComparison.Ordinal);

    ///<inheritdoc/>
    public string IdentityName(string identity) {
      var schema= IdentityConstants.ApplicationScheme + '/';
      if (identity.StartsWith(schema, StringComparison.Ordinal)) return identity.Substring(schema.Length);
      return identity;
    }

    //Consecutive failed logins
    internal class FailedLogin {
      public int Count { get; private set; }
      public DateTime LockoutUntil { get; private set; }
      public bool IsLockedOut() => Tlabs.App.TimeInfo.Now < LockoutUntil;
      public FailedLogin Increment() {
        /* Lockout per failed attempt:
         *  N      sec.   min.
            1:       5
            2:      23
            3:     111      2
            4:     535      9
            5:    2576     43
            6:   12392    207
            7:   59610    993
            8:  286751   4779
            9: 1379411  22990
           10: 6635624 110594
        */
        lock(this) {
          LockoutUntil= Tlabs.App.TimeInfo.Now.AddSeconds(Math.Exp(Math.PI/2 * ++Count)); //exponential growth of lockout time
          return this;
        }
      }
    }

    ///<summary>Default lookup normalizer.</summary>
    public class DefaultNormalizer : ILookupNormalizer {
      ///<summary>Normalize <paramref name="email"/>.</summary>
      public string NormalizeEmail(string email) {
        if (null == email) return email;
        return email.Normalize().ToLowerInvariant();
      }

      ///<summary>Normalize <paramref name="name"/>.</summary>
      public string NormalizeName(string name) {
        if (null == name) return name;
        return name.Normalize().ToUpperInvariant();
      }
    }

  }

}
