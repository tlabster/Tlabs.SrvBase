using System.Collections.Generic;
using Tlabs.Data.Model;

namespace Tlabs.Identity {

  ///<summary>Roles administration service interface.</summary>
  public interface IRolesAdministration {

    ///<summary>List of <see cref="User"/>(s) matching optional <paramref name="filterName"/>.</summary>
    IList<Role> FilteredList(string filterName= null);
    
    ///<summary>Return role by <paramref name="name"/>.</summary>
    Role GetByName(string name);

    ///<summary>Create or update existing <paramref name="role"/>.</summary>
    Role CreateOrUpdate(Role role);

    ///<summary>Delete role with <paramref name="roleName"/>.</summary>
    void Delete(string roleName);
  }
}