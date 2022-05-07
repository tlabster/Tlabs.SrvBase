using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Tlabs.Config;

namespace Tlabs.Data {

  ///<summary>Validate data-store configuration.</summary>
  public class DataStoreValidationConfigurator : IConfigurator<MiddlewareContext> {
  ///<inheritdoc/>
    public void AddTo(MiddlewareContext mware, IConfiguration cfg) {
      Tlabs.App.WithServiceScope(svcProv => {
        var dStore= svcProv.GetRequiredService<IDataStore>();
        var dataSeeds= svcProv.GetServices<IDataSeed>();  //all registered data seeding services
        dStore.EnsureStore(dataSeeds);
      });
    }
  }
}