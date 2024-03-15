using System.Reflection;
using System.Collections.Generic;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;


namespace Tlabs.Config {

  ///<summary>Configures development user secrets.</summary>
  public class UserSecretsConfigurastor : IConfigurator<IWebHostBuilder> {
    ///<summary>Use regex config name</summary>
    static readonly ILogger log= App.Logger<UserSecretsConfigurastor>();
    readonly IDictionary<string, string> config;

    ///<summary>Default ctor.</summary>
    public UserSecretsConfigurastor() : this(null) { }

    ///<summary>Ctor from <paramref name="config"/>.</summary>
    public UserSecretsConfigurastor(IDictionary<string, string>? config) {
      this.config= config ?? new Dictionary<string, string>();
    }

    ///<inheritdoc/>
    public void AddTo(IWebHostBuilder webHostBuilder, IConfiguration cfg) =>
      webHostBuilder.ConfigureAppConfiguration((ctx, cfg) => cfg.AddUserSecrets(Assembly.GetEntryAssembly() ?? Assembly.Load(Tlabs.App.Setup.Name)));
  }
}