using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Tlabs.Data;
using Tlabs.Data.Entity;
using Tlabs.Server.Model;
using Xunit;


namespace Tlabs.Identity.Intern.Test {

  [Collection("App.ServiceProv")]   //All tests of classes with this same collection name do never run in parallel /https://xunit.net/docs/running-tests-in-parallel)
  public class DefaultApiKeyRegistryTests : IClassFixture<DefaultApiKeyRegistryTests.Fixture> {
    public class MockPasswordHasher : IPasswordHasher<string> {
      public string HashPassword(string user, string password) => $"{user}/{password}";

      public PasswordVerificationResult VerifyHashedPassword(string user, string hashedPassword, string providedPassword) {
        var hash= hashedPassword.Split('/');
        return user == hash[0] && hash[1] ==  providedPassword ? PasswordVerificationResult.Success : PasswordVerificationResult.Failed;
      }
    }
    public class Fixture : IDisposable {
      public const string GENERATED_KEY= "GENERATED_KEY";
      public IApiKeyRegistry ApiKeyRegistry { get; }
      public IRepo<ApiKey> ApiKeyRepo { get; }
      public IList<ApiKey> DataStore { get; }
      public IPasswordHasher<string> PasswordHasher { get; }
      public IServiceProvider SvcProvider { get; }
      public IServiceProvider SvcProviderScope { get; set; }
      public IServiceScope SvcScope { get; }
      public IServiceScopeFactory SvcScopeFactory { get; }
      public IOptions<SingletonApiKeyDataStoreRegistry.Options> Options { get; }
      IServiceProvider saveSvcProv;

      public Fixture() {
        this.saveSvcProv= App.ServiceProv;
        this.PasswordHasher= new MockPasswordHasher();
        var store= new Mock<IUserStore<User>>();

        var role= new Role {
          Name= "Admin"
        };

        this.DataStore= new List<ApiKey>();

        var k= new ApiKey {
          Id= 1,
          TokenName= "ValidToken1",
          Hash= "ValidToken1/Valid1",
          Description= "Valid Token 1",
          ValidFrom= default(DateTime),
          ValidUntil= null,
          ValidityState= ApiKey.Status.ACTIVE.ToString()
        };
        k.Roles= new List<ApiKey.RoleRef> {
          new ApiKey.RoleRef {
            Role= role,
            ApiKey= k
          }
        };
        this.DataStore.Add(k);


        k= new ApiKey {
          Id= 2,
          TokenName= "ValidToken2",
          Hash= "ValidToken2/Valid2",
          Description= "Valid Token 2",
          ValidFrom= default(DateTime),
          ValidUntil= App.TimeInfo.Now.Date.AddMonths(1),
          ValidityState= ApiKey.Status.ACTIVE.ToString()
        };
        k.Roles= new List<ApiKey.RoleRef> {
          new ApiKey.RoleRef {
            Role= role,
            ApiKey= k
          }
        };
        this.DataStore.Add(k);

        k= new ApiKey {
          Id= 3,
          TokenName= "ExpiredToken1",
          Hash= "ExpiredToken1/Expired1",
          Description= "Expired Token 1",
          ValidFrom= default(DateTime),
          ValidUntil= App.TimeInfo.Now.Date.AddMonths(-1),
          ValidityState= ApiKey.Status.ACTIVE.ToString()
        };
        k.Roles= new List<ApiKey.RoleRef> {
          new ApiKey.RoleRef {
            Role= role,
            ApiKey= k
          }
        };
        this.DataStore.Add(k);

        k= new ApiKey {
          Id= 4,
          TokenName= "NotYetValidToken1",
          Hash= "NotYetValidToken1/NotYetValid1",
          Description= "Not Yet Valid Token 1",
          ValidFrom= App.TimeInfo.Now.Date.AddMonths(1),
          ValidUntil= null,
          ValidityState= ApiKey.Status.ACTIVE.ToString()
        };
        k.Roles= new List<ApiKey.RoleRef> {
          new ApiKey.RoleRef {
            Role= role,
            ApiKey= k
          }
        };
        this.DataStore.Add(k);

        k= new ApiKey {
          Id= 5,
          TokenName= "DeletedToken1",
          Hash= "DeletedToken1/Deleted1",
          Description= "Deleted Token 1",
          ValidFrom= default(DateTime),
          ValidUntil= null,
          ValidityState= ApiKey.Status.DELETED.ToString()
        };
        k.Roles= new List<ApiKey.RoleRef> {
          new ApiKey.RoleRef {
            Role= role,
            ApiKey= k
          }
        };
        this.DataStore.Add(k);

        k= new ApiKey {
          Id= 6,
          TokenName= "DeletedToken2",
          Hash= "DeletedToken2/Deleted2",
          Description= "Deleted Token 2",
          ValidFrom= default(DateTime),
          ValidUntil= App.TimeInfo.Now.Date.AddMonths(-1),
          ValidityState= ApiKey.Status.DELETED.ToString()
        };
        k.Roles= new List<ApiKey.RoleRef> {
          new ApiKey.RoleRef {
            Role= role,
            ApiKey= k
          }
        };
        this.DataStore.Add(k);


        var apiKeyRepoMock= new Mock<IRepo<ApiKey>>();
        apiKeyRepoMock.Setup(s => s.All)
                      .Returns(this.DataStore.AsQueryable());
        apiKeyRepoMock.Setup(s => s.AllUntracked)
                      .Returns(this.DataStore.AsQueryable());

        var emptyStore= new NoopStoreConfigurator.NoopDataStore();
        apiKeyRepoMock.Setup(s => s.Store).Returns(emptyStore);

        apiKeyRepoMock.Setup(s => s.Insert(It.IsAny<ApiKey>()))
                      .Callback<ApiKey>(key => this.DataStore.Add(key))
                      .Returns<ApiKey>(key => key);
        apiKeyRepoMock.Setup(s => s.Update(It.IsAny<ApiKey>()))
                      .Callback<ApiKey>(key => {
                        var obj= this.DataStore.First(r => r.TokenName == key.TokenName);
                        obj.ValidUntil= key.ValidUntil; //only these two properties are updated (if in the future more is updated, need to change this)
                        obj.ValidityState= key.ValidityState;
                      })
                      .Returns<ApiKey>(key => key);

        this.ApiKeyRepo= apiKeyRepoMock.Object;

        var svcProvScp= new Mock<IServiceProvider>();
        svcProvScp.Setup(r => r.GetService(It.Is<Type>(t => t == typeof(IPasswordHasher<string>)))).Returns(this.PasswordHasher);
        svcProvScp.Setup(r => r.GetService(It.Is<Type>(t => t == typeof(IRepo<ApiKey>)))).Returns(this.ApiKeyRepo);
        this.SvcProviderScope= svcProvScp.Object;

        var svcScp= new Mock<IServiceScope>();
        svcScp.Setup(r => r.ServiceProvider).Returns(this.SvcProviderScope);
        this.SvcScope= svcScp.Object;

        var svcFac= new Mock<IServiceScopeFactory>();
        svcFac.Setup(r => r.CreateScope()).Returns(this.SvcScope);
        this.SvcScopeFactory= svcFac.Object;

        var svcProv= new Mock<IServiceProvider>();
        svcProv.Setup(r => r.GetService(It.Is<Type>(t => t == typeof(IServiceScopeFactory)))).Returns(this.SvcScopeFactory);
        this.SvcProvider= svcProv.Object;

        App.Setup= App.Setup with { ServiceProv= this.SvcProvider };

        var options= new Mock<IOptions<SingletonApiKeyDataStoreRegistry.Options>>();
        options.Setup(o => o.Value).Returns(new SingletonApiKeyDataStoreRegistry.Options {
          initialKey= Guid.Empty.ToString(),
          initialTokenName= "INITIAL",
          initialValidHours= 1,
          genKeyLength= 32
        });
        this.Options= options.Object;

        this.ApiKeyRegistry= new SingletonApiKeyDataStoreRegistry(this.Options);
      }

      public void Dispose() {
        App.Setup= App.Setup with { ServiceProv= this.saveSvcProv };
      }
    }

    private Fixture fixture;
    private IApiKeyRegistry registry;
    public DefaultApiKeyRegistryTests(Fixture fixture) {
      this.fixture= fixture;
      this.registry= fixture.ApiKeyRegistry;
    }

    [Fact]
    public void TestGenerateKey() {
      var key= registry.GenerateKey();
      Assert.True(key.Length > 0);
    }

    [Fact]
    public void TestVerifyKey() {
      //First attempt should be non-cached, second should be cached
      var key1= registry.VerifiedKey("Valid1");
      Assert.NotNull(key1);
      Assert.True(key1.TokenName == "ValidToken1");
      key1= registry.VerifiedKey("Valid1");
      Assert.NotNull(key1);
      Assert.True(key1.TokenName == "ValidToken1");

      var key2= registry.VerifiedKey("Valid2");
      Assert.NotNull(key2);
      Assert.True(key2.TokenName == "ValidToken2");
      key2= registry.VerifiedKey("Valid2");
      Assert.NotNull(key2);
      Assert.True(key2.TokenName == "ValidToken2");
      //reset valid until in cached object to before, in data store it will still be correct
      key2.ValidUntil= App.TimeInfo.Now.AddMonths(-1);
      key2= registry.VerifiedKey("Valid2");
      Assert.NotNull(key2);
      Assert.True(key2.TokenName == "ValidToken2");

      var key3= registry.VerifiedKey("Expired1");
      Assert.Null(key3);

      var key4= registry.VerifiedKey("NotYetValid1");
      Assert.Null(key4);

      var key5= registry.VerifiedKey("Deleted1");
      Assert.Null(key5);

      var key6= registry.VerifiedKey("Deleted2");
      Assert.Null(key6);
    }

    [Fact]
    public void TestRegisteredKeys() {
      var keys= registry.RegisteredKeys();
      Assert.True(keys.Count() >= 4);
      Assert.True(keys[0].TokenName == "ValidToken1");
    }
    [Fact]
    public void TestRegisterKey() {
      var key= "RegisteredKey1";
      var token= registry.Register(new KeyToken{
        TokenName= key,
        Description= key,
        Roles= new List<string> { "Admin" }
      }, key);
      Assert.NotNull(token);
      Assert.True(token.TokenName == key);
      Assert.True(token.Description == key);

      token= registry.VerifiedKey(key);
      Assert.NotNull(token);
      Assert.True(token.TokenName == key);
    }

    [Fact]
    public void TestDeregisterKey() {
      var key= "DeregisteredKey1";
      var token= registry.Register(
        new KeyToken{
          TokenName= key,
          Description= key,
          Roles= new List<string> { "Admin" }
        }, key
      );
      Assert.NotNull(token);
      Assert.Equal(key, token.TokenName);
      Assert.Equal(key, token.Description);

      token= registry.VerifiedKey(key);
      Assert.NotNull(token);
      Assert.True(token.TokenName == key);

      token= registry.Deregister(key);
      Assert.NotNull(token);
      Assert.NotEqual(key, token.TokenName);  //TokenName must be changed

      token= registry.VerifiedKey(key);
      Assert.Null(token);
    }

    [Fact]
    public void TestValidation() {
      Assert.Throws<ArgumentNullException>(() => registry.Register(new KeyToken { TokenName= "", Description= "Test", Roles= new List<string> { "Admin" }}, "bla"));
      Assert.Throws<ArgumentNullException>(() => registry.Register(new KeyToken { TokenName= "Test", Description= "Test", Roles= new List<string> { "Admin" } }, ""));
      Assert.Throws<ArgumentNullException>(() => registry.Register(new KeyToken { TokenName= "Test", Description= "Test" }, "key"));
    }
  }
}