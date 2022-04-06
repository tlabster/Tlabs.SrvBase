using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

using Tlabs.Data;
using Tlabs.Data.Model;
using Tlabs.Misc;

namespace Tlabs.Identity.Intern {

  ///<summary>Roles administration singleton service.</summary>
  public class SingletonRolesAdministration : IRolesAdministration {
    class LazyCache {
      internal static ICache<string, Role> instance;
      static LazyCache() {
        instance= new LookupCache<string, Role>(internalAllPersistentRoles());
        log.LogInformation("Roles cache initialized with {cnt} roles.", instance.Entries.Count());
      }
    }

    static readonly ILogger log= App.Logger<SingletonRolesAdministration>();
    static ICache<string, Role> cache => LazyCache.instance;
    readonly ILookupNormalizer norm= new UserAdministration.DefaultNormalizer();

    ///<inheritdoc/>
    public IQueryable<Role> FilteredRoles(QueryFilter filter) {
      var ret= default(List<Role>);
      Tlabs.App.WithServiceScope(prov => {
        var store = prov.GetService<IDataStore>();
        var query = store.UntrackedQuery<Tlabs.Data.Entity.Role>();
        query = filter.Apply(query, filterMap, sorterMap);
        ret= query.Select(r => new Role(r)).ToList(); // Load in memory to prevent out of context error
      });
      return ret.AsQueryable();
    }
    ///<inheritdoc/>
    public IList<Role> FilteredList(string nameFilter= null)
      => cache.Entries.Where(p => string.IsNullOrEmpty(nameFilter) || p.Key.StartsWith(norm.NormalizeName(nameFilter)))
                            .Select(p => p.Value)
                            .ToList();

    ///<inheritdoc/>
    public Data.Model.Role GetByName(string name) {
      var role= cache[norm.NormalizeName(name)];
      if (null == role) throw new DataEntityNotFoundException<Data.Entity.Role>(name);
      return role;
    }

    ///<inheritdoc/>
    public Role CreateOrUpdate(Data.Model.Role role) => cache[norm.NormalizeName(role.Key)]= internalCreateOrUpdate(role);

    ///<inheritdoc/>
    public void Delete(string roleName) => cache.Evict(internalDelete(roleName));

    static T ReturnFrom<T>(Func<ILookupNormalizer, RoleManager<Data.Entity.Role>, IDataStore, T> doWith) {
      T ret= default;
      Tlabs.App.WithServiceScope(prov => {
        var norm= prov.GetService<ILookupNormalizer>() ?? new UserAdministration.DefaultNormalizer();
        var rmngr= prov.GetService<RoleManager<Data.Entity.Role>>();
        var store= prov.GetService<IDataStore>();
        ret= doWith(norm, rmngr, store);
      });
      return ret;
    }

    static IEnumerable<KeyValuePair<string, Role>> internalAllPersistentRoles() => ReturnFrom((norm, roleMngr, store)
      => { try {
          return roleMngr.Roles.Select(r => new KeyValuePair<string, Role>(norm.NormalizeName(r.Name), new Role(r))).ToList();
        }
        catch (Exception e) {
          log.LogError(e, "Failed to load persistent roles.");
          throw;
        }
      });

    static Role internalCreateOrUpdate(Role role) => ReturnFrom((norm, roleMngr, store) => {
      var existing= roleMngr.Roles.SingleOrDefault(r => r.NormalizedRoleName == norm.NormalizeName(role.Key));
      if (null != existing) {
        roleMngr.UpdateAsync(role.CopyTo(existing)).GetAwaiter().GetResult();
      }
      else roleMngr.CreateAsync(existing= role.AsEntity()).GetAwaiter().GetResult();
      return new Role(existing);
    });

    static string internalDelete(string roleName) => ReturnFrom((norm, roleMngr, store) => {
      var nrn= norm.NormalizeName(roleName);
      if (store.UntrackedQuery<Data.Entity.User.RoleRef>().Any(r => r.Role.Name == nrn))
        throw Tlabs.EX.New<ArgumentException>("Role '{name}' cannot be deleted (still assigned to users)", roleName);
      var role= roleMngr.Roles.SingleEntity(r => r.NormalizedRoleName == nrn, roleName);
      roleMngr.DeleteAsync(role).GetAwaiter().GetResult();
      return nrn;
    });

    static readonly IDictionary<string, QueryFilter.FilterExpression<Tlabs.Data.Entity.Role>> filterMap =
      new Dictionary<string, QueryFilter.FilterExpression<Tlabs.Data.Entity.Role>>(StringComparer.OrdinalIgnoreCase) {
        [nameof(Role.Key)] = (q, cv) => q.Where(r => r.Name.StartsWith(cv.ToString())),
        [nameof(Role.Description)] = (q, cv) => q.Where(r => r.Description.Contains(cv.ToString())),
      };

    static readonly IDictionary<string, QueryFilter.SorterExpression<Tlabs.Data.Entity.Role>> sorterMap =
    new Dictionary<string, QueryFilter.SorterExpression<Tlabs.Data.Entity.Role>>(StringComparer.OrdinalIgnoreCase) { };

  }

}