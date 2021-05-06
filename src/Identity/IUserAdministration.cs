
using System.Security.Claims;
using System.Threading.Tasks;

using Tlabs.Data.Model;

namespace Tlabs.Identity {

  ///<summary>User administration service interface.</summary>
  public interface IUserAdministration {

    ///<summary>List of <see cref="User"/>(s) matching optional <paramref name="filter"/>.</summary>
    IResultList<User> FilteredList(QueryFilter filter= null);
    
    ///<summary>Return user by <paramref name="userName"/>.</summary>
    User GetByName(string userName);

    ///<summary>Return user by <paramref name="email"/>.</summary>
    User GetByEmail(string email);

    ///<summary>Create <paramref name="user"/>.</summary>
    void Create(User user);

    ///<summary>Update <paramref name="user"/>.</summary>
    void Update(User user);

    ///<summary>Delete user with <paramref name="userName"/>.</summary>
    void Delete(string userName);

    ///<summary>True if nomalized keys are equal.</summary>
    bool NomalizedNameEquals(string k1, string k2);

    ///<summary>Returns the parsed user name of an identity.</summary>
    string IdentityName(string identity);

    ///<summary>Return logged-in user identified by <paramref name="principal"/>.</summary>
    User GetLoggedIn(ClaimsPrincipal principal);

    ///<summary>Login user with <paramref name="userName"/> and <paramref name="pwd"/> with support of second-factor .</summary>
    Task<LoginResult> Login(string userName, string pwd);

    ///<summary>Login user <paramref name="userName"/> with second factor <paramref name="token"/>.</summary>
    Task<LoginResult> SecondFactorLogin(string userName, string token);

    ///<summary>Logoff current user.</summary>
    void LogoffCurrent();
  }

  ///<summary>Login result.</summary>
  public enum LoginResult {
    ///<summary>Login succeeded.</summary>
    SUCCESS,
    ///<summary>Login failed with invalid credentials.</summary>
    FAILED,
    ///<summary>Second factor challange required for login missing.</summary>
    SECOND_FACTOR_MISSING,
    ///<summary>User locked off.</summary>
    LOCKED_OFF,
    ///<summary>Account is deactivated.</summary>
    DEACTIVATED
  }

}