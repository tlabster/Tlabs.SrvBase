using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tlabs.Data;
using Tlabs.Data.Entity;
using Microsoft.AspNetCore.Identity;
using Task = System.Threading.Tasks.Task;

namespace Tlabs.Server.Identity {

  ///<summary>>see cref="User"/> spcific repository implementation.</summary>
  public class UserIdentityStore : IUserStore<User>, IUserPasswordStore<User>, IUserEmailStore<User>, IUserRoleStore<User> {
    private bool _disposed;

    private IRepo<User> repo;
    /// <summary>
    /// Ctor from user repo
    /// </summary>
    /// <param name="repo">User repository</param>
    public UserIdentityStore(IRepo<User> repo) {
      this.repo= repo;
    }

    /// <inherit/>
    public Task<IdentityResult> CreateAsync(User user, CancellationToken cancellationToken) {
      cancellationToken.ThrowIfCancellationRequested();
      ThrowIfDisposed();
      repo.Insert(user);
      repo.Store.CommitChanges();
      return Task.FromResult(IdentityResult.Success);
    }

    /// <inherit/>
    public Task<IdentityResult> DeleteAsync(User user, CancellationToken cancellationToken) {
      cancellationToken.ThrowIfCancellationRequested();
      ThrowIfDisposed();
      repo.Delete(user);
      repo.Store.CommitChanges();
      return Task.FromResult(IdentityResult.Success);
    }

    /// <inherit/>
    public Task<User> FindByIdAsync(string userId, CancellationToken cancellationToken) {
      cancellationToken.ThrowIfCancellationRequested();
      ThrowIfDisposed();
      if (int.TryParse(userId, out int id))
        return Task.FromResult(repo.Get(id));
        
      return Task.FromResult((User)null);
    }

    /// <inherit/>
    public Task<User> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken) {
      cancellationToken.ThrowIfCancellationRequested();
      ThrowIfDisposed();
      return Task.FromResult(repo.AllUntracked.SingleOrDefault(x => x.NormalizedUserName == normalizedUserName));
    }

    /// <inherit/>
    public Task<string> GetNormalizedUserNameAsync(User user, CancellationToken cancellationToken) {
      cancellationToken.ThrowIfCancellationRequested();
      ThrowIfDisposed();
      return Task.FromResult(user.NormalizedUserName);
    }

    /// <inherit/>
    public Task<string> GetPasswordHashAsync(User user, CancellationToken cancellationToken) {
      cancellationToken.ThrowIfCancellationRequested();
      ThrowIfDisposed();
      return Task.FromResult(user.PasswordHash);
    }

    /// <inherit/>
    public Task<string> GetUserIdAsync(User user, CancellationToken cancellationToken) {
      cancellationToken.ThrowIfCancellationRequested();
      ThrowIfDisposed();
      return Task.FromResult(user.Id.ToString());
    }

    /// <inherit/>
    public Task<string> GetUserNameAsync(User user, CancellationToken cancellationToken) {
      cancellationToken.ThrowIfCancellationRequested();
      ThrowIfDisposed();
      return Task.FromResult(user.UserName);
    }

    /// <inherit/>
    public Task<bool> HasPasswordAsync(User user, CancellationToken cancellationToken) {
      cancellationToken.ThrowIfCancellationRequested();
      ThrowIfDisposed();
      return Task.FromResult(!string.IsNullOrWhiteSpace(user.PasswordHash));
    }

    /// <inherit/>
    public Task SetNormalizedUserNameAsync(User user, string normalizedName, CancellationToken cancellationToken) {
      cancellationToken.ThrowIfCancellationRequested();
      ThrowIfDisposed();
      user.NormalizedUserName= normalizedName;
      return Task.FromResult((object) null);
    }

    /// <inherit/>
    public Task SetPasswordHashAsync(User user, string passwordHash, CancellationToken cancellationToken) {
      cancellationToken.ThrowIfCancellationRequested();
      ThrowIfDisposed();
      user.PasswordHash= passwordHash;
      return Task.FromResult((object) null);
    }

    /// <inherit/>
    public System.Threading.Tasks.Task SetUserNameAsync(User user, string userName, CancellationToken cancellationToken) {
      cancellationToken.ThrowIfCancellationRequested();
      ThrowIfDisposed();
      user.UserName= userName;
      return Task.FromResult((object) null);
    }

    /// <inherit/>
    public Task<IdentityResult> UpdateAsync(User user, CancellationToken cancellationToken) {
      cancellationToken.ThrowIfCancellationRequested();
      ThrowIfDisposed();
      repo.Update(user);
      repo.Store.CommitChanges();
      return Task.FromResult(IdentityResult.Success);
    }

    /// <inherit/>
    public Task SetEmailAsync(User user, string email, CancellationToken cancellationToken) {
      cancellationToken.ThrowIfCancellationRequested();
      ThrowIfDisposed();
      user.Email= email;
      return Task.FromResult((object) null);
    }

    /// <inherit/>
    public Task<string> GetEmailAsync(User user, CancellationToken cancellationToken) {
      cancellationToken.ThrowIfCancellationRequested();
      ThrowIfDisposed();
      return Task.FromResult(user.Email);
    }

    /// <inherit/>
    public Task<bool> GetEmailConfirmedAsync(User user, CancellationToken cancellationToken) {
      cancellationToken.ThrowIfCancellationRequested();
      ThrowIfDisposed();
      return Task.FromResult(user.EmailConfirmed);
    }

    /// <inherit/>
    public Task SetEmailConfirmedAsync(User user, bool confirmed, CancellationToken cancellationToken) {
      cancellationToken.ThrowIfCancellationRequested();
      ThrowIfDisposed();
      user.EmailConfirmed= confirmed;
      return Task.FromResult((object) null);
    }

    /// <inherit/>
    public Task<User> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) {
      cancellationToken.ThrowIfCancellationRequested();
      ThrowIfDisposed();
      return Task.FromResult(repo.AllUntracked.SingleOrDefault(x => x.NormalizedEmail == normalizedEmail));
    }

    /// <inherit/>
    public Task<string> GetNormalizedEmailAsync(User user, CancellationToken cancellationToken) {
      cancellationToken.ThrowIfCancellationRequested();
      ThrowIfDisposed();
      return Task.FromResult(user.NormalizedEmail);
    }

    /// <inherit/>
    public Task SetNormalizedEmailAsync(User user, string normalizedEmail, CancellationToken cancellationToken) {
      cancellationToken.ThrowIfCancellationRequested();
      ThrowIfDisposed();
      user.NormalizedEmail= normalizedEmail;
      return Task.FromResult((object) null);
    }

    /// <inherit/>
    public Task AddToRoleAsync(User user, string roleName, CancellationToken cancellationToken) {
      cancellationToken.ThrowIfCancellationRequested();
      ThrowIfDisposed();
      var role= repo.Store.Query<Role>().First(r => r.Name == roleName);
      repo.Store.Insert<User.RoleRef>(new User.RoleRef { User= user, Role= role });
      return Task.CompletedTask;
    }

    /// <inherit/>
    public Task RemoveFromRoleAsync(User user, string roleName, CancellationToken cancellationToken) {
      cancellationToken.ThrowIfCancellationRequested();
      ThrowIfDisposed();
      var userRole= repo.Store.Query<User.RoleRef>().First(r => r.User.Id == user.Id && r.Role.Name == roleName);
      repo.Store.Delete<User.RoleRef>(userRole);
      return Task.CompletedTask;
    }

    /// <inherit/>
    public Task<IList<string>> GetRolesAsync(User user, CancellationToken cancellationToken) {
      cancellationToken.ThrowIfCancellationRequested();
      ThrowIfDisposed();
      IList<string> roleNames= repo.Store.Query<User.RoleRef>().Where(r => r.User.Id == user.Id).Select(r => r.Role.Name).ToList();
      return Task.FromResult(roleNames);
    }

    /// <inherit/>
    public Task<bool> IsInRoleAsync(User user, string roleName, CancellationToken cancellationToken) {
      cancellationToken.ThrowIfCancellationRequested();
      ThrowIfDisposed();
      return Task.FromResult(repo.Store.Query<User.RoleRef>().Any(ur => ur.Role.Name == roleName && ur.User.Id == user.Id));
    }

    /// <inherit/>
    public Task<IList<User>> GetUsersInRoleAsync(string roleName, CancellationToken cancellationToken) {
      cancellationToken.ThrowIfCancellationRequested();
      ThrowIfDisposed();
      IList<User> users= repo.AllUntracked.Where(u => u.Roles.Select(r => r.Role.Name).Contains(roleName)).ToList();
      return Task.FromResult(users);
    }

    /// <inherit/>
    public void Dispose() {
      _disposed= true;
    }

    /// <summary>
    /// Throws an exception if the object was already disposed
    /// </summary>
    protected void ThrowIfDisposed() {
      if (_disposed) {
        throw new ObjectDisposedException(GetType().Name);
      }
    }
  }
}