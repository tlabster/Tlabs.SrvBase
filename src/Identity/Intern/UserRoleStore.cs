using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tlabs.Data;
using Tlabs.Data.Entity;
using Microsoft.AspNetCore.Identity;
using Task = System.Threading.Tasks.Task;

namespace Tlabs.Identity.Intern {
  ///<summary>User roles store</summary>
  public class UserRoleStore : IRoleStore<Role>, IQueryableRoleStore<Role> {
    /// <summary>Role repository</summary>
    public IRepo<Role> repo;

    /// <summary>
    /// Ctor from Repo
    /// </summary>
    /// <param name="repo">Role repository</param>
    public UserRoleStore(IRepo<Role> repo) {
      this.repo= repo;
    }

    /// <inherit/>
    public IQueryable<Role> Roles => repo.AllUntracked;

    /// <inherit/>
    public Task<IdentityResult> CreateAsync(Role role, CancellationToken cancellationToken) {
      cancellationToken.ThrowIfCancellationRequested();
      ThrowIfDisposed();
      repo.Insert(role);
      repo.Store.CommitChanges();
      return Task.FromResult(IdentityResult.Success);
    }

    /// <inherit/>
    public Task<IdentityResult> DeleteAsync(Role role, CancellationToken cancellationToken) {
      cancellationToken.ThrowIfCancellationRequested();
      ThrowIfDisposed();
      repo.Delete(role);
      repo.Store.CommitChanges();
      return Task.FromResult(IdentityResult.Success);
    }

    /// <inherit/>
    public Task<Role> FindByIdAsync(string roleId, CancellationToken cancellationToken) {
      cancellationToken.ThrowIfCancellationRequested();
      ThrowIfDisposed();
      if (int.TryParse(roleId, out int id))
        return Task.FromResult(repo.Get(id));

      return Task.FromResult((Role)null);
    }

    /// <inherit/>
    public Task<Role> FindByNameAsync(string normalizedRoleName, CancellationToken cancellationToken) {
      cancellationToken.ThrowIfCancellationRequested();
      ThrowIfDisposed();
      return Task.FromResult(repo.AllUntracked.SingleOrDefault(x => x.NormalizedRoleName == normalizedRoleName));
    }

    /// <inherit/>
    public Task<string> GetNormalizedRoleNameAsync(Role role, CancellationToken cancellationToken) {
      cancellationToken.ThrowIfCancellationRequested();
      ThrowIfDisposed();
      return Task.FromResult(role.NormalizedRoleName);
    }

    /// <inherit/>
    public Task<string> GetRoleIdAsync(Role role, CancellationToken cancellationToken) {
      cancellationToken.ThrowIfCancellationRequested();
      ThrowIfDisposed();
      return Task.FromResult(role.Id.ToString());
    }

    /// <inherit/>
    public Task<string> GetRoleNameAsync(Role role, CancellationToken cancellationToken) {
      cancellationToken.ThrowIfCancellationRequested();
      ThrowIfDisposed();
      return Task.FromResult(role.Name);
    }

    /// <inherit/>
    public System.Threading.Tasks.Task SetNormalizedRoleNameAsync(Role role, string normalizedName, CancellationToken cancellationToken) {
      cancellationToken.ThrowIfCancellationRequested();
      ThrowIfDisposed();
      role.NormalizedRoleName= normalizedName;
      return Task.CompletedTask;
    }

    /// <inherit/>
    public System.Threading.Tasks.Task SetRoleNameAsync(Role role, string roleName, CancellationToken cancellationToken) {
      cancellationToken.ThrowIfCancellationRequested();
      ThrowIfDisposed();
      role.Name= roleName;
      return Task.CompletedTask;
    }

    /// <inherit/>
    public Task<IdentityResult> UpdateAsync(Role role, CancellationToken cancellationToken) {
      cancellationToken.ThrowIfCancellationRequested();
      ThrowIfDisposed();
      repo.Update(role);
      repo.Store.CommitChanges();
      return Task.FromResult(IdentityResult.Success);
    }

    /// <inherit/>
    public void Dispose() => this.repo= null;

    /// <summary>
    /// Throws an exception if the object was already disposed
    /// </summary>
    protected void ThrowIfDisposed() {
      if (null == repo) throw new ObjectDisposedException(nameof(UserRoleStore));
    }
  }
}