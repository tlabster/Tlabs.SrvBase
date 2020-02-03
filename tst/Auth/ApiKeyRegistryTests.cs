using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Tlabs.Data;
using Tlabs.Data.Entity;
using Tlabs.Server.Auth;
using Xunit;
using Xunit.Abstractions;


namespace Tlabs.Server.Controller.Test {

  public class DefaultApiKeyRegistryTests : IClassFixture<DefaultApiKeyRegistryTests.Fixture> {
    public class MockPasswordHasher : IPasswordHasher<User> {
      public string HashPassword(User user, string password) => password;

      public PasswordVerificationResult VerifyHashedPassword(User user, string hashedPassword, string providedPassword) {
        return hashedPassword == providedPassword ? PasswordVerificationResult.Success : PasswordVerificationResult.Failed;
      }
    }
    public class Fixture {
      public const string GENERATED_KEY= "GENERATED_KEY";
      public IApiKeyRegistry ApiKeyRegistry { get; }
      public IRepo<ApiKey> ApiKeyRepo { get; }
      public UserManager<User> UserManager { get; }
      public IList<ApiKey> DataStore { get; }
      public IPasswordHasher<User> PasswordHasher { get; }
      public IServiceProvider SvcProvider { get; }
      public IServiceProvider SvcProviderScope { get; set; }
      public IServiceScope SvcScope { get; }
      public IServiceScopeFactory SvcScopeFactory { get; }
      public Fixture() {
        this.PasswordHasher= new MockPasswordHasher();
        var store = new Mock<IUserStore<User>>();
        var userManagerMock = new Mock<UserManager<User>>(store.Object, null, null, null, null, null, null, null, null);
        userManagerMock.Object.PasswordHasher= this.PasswordHasher;
        UserManager= userManagerMock.Object;

        this.DataStore= new List<ApiKey>{
          new ApiKey {
            Id= 1,
            TokenName= "ValidToken1",
            Hash= "ValidToken1",
            Description= "Valid Token 1",
            ValidFrom= default(DateTime),
            ValidUntil= null,
            ValidityState= ApiKey.Status.ACTIVE.ToString()
          }, new ApiKey {
            Id= 2,
            TokenName= "ValidToken2",
            Hash= "ValidToken2",
            Description= "Valid Token 2",
            ValidFrom= default(DateTime),
            ValidUntil= App.TimeInfo.Now.Date.AddMonths(1),
            ValidityState= ApiKey.Status.ACTIVE.ToString()
          }, new ApiKey {
            Id= 3,
            TokenName= "ExpiredToken1",
            Hash= "ExpiredToken1",
            Description= "Expired Token 1",
            ValidFrom= default(DateTime),
            ValidUntil= App.TimeInfo.Now.Date.AddMonths(-1),
            ValidityState= ApiKey.Status.ACTIVE.ToString()
          }, new ApiKey {
            Id= 4,
            TokenName= "NotYetValidToken1",
            Hash= "NotYetValidToken1",
            Description= "Not Yet Valid Token 1",
            ValidFrom= App.TimeInfo.Now.Date.AddMonths(1),
            ValidUntil= null,
            ValidityState= ApiKey.Status.ACTIVE.ToString()
          }, new ApiKey {
            Id= 5,
            TokenName= "DeletedToken1",
            Hash= "DeletedToken1",
            Description= "Deleted Token 1",
            ValidFrom= default(DateTime),
            ValidUntil= null,
            ValidityState= ApiKey.Status.DELETED.ToString()
          },
          new ApiKey {
            Id= 6,
            TokenName= "DeletedToken2",
            Hash= "DeletedToken2",
            Description= "Deleted Token 2",
            ValidFrom= default(DateTime),
            ValidUntil= App.TimeInfo.Now.Date.AddMonths(-1),
            ValidityState= ApiKey.Status.DELETED.ToString()
          }
        };
        var apiKeyRepoMock= new Mock<IRepo<ApiKey>>();
        apiKeyRepoMock.Setup(s => s.AllUntracked)
                      .Returns(this.DataStore.AsQueryable());
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
        apiKeyRepoMock.Setup(r => r.Store.CommitChanges()).Callback(() => Thread.Sleep(50)/*do nothing*/);

        this.ApiKeyRepo= apiKeyRepoMock.Object;

        var svcProvScp= new Mock<IServiceProvider>();
        svcProvScp.Setup(r => r.GetService(It.Is<Type>(t => t == typeof(UserManager<User>)))).Returns(this.UserManager);
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

        App.ServiceProv= this.SvcProvider;

        this.ApiKeyRegistry= new DefaultApiKeyRegistry();
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
      var key1= registry.VerifiedKey("ValidToken1");
      Assert.NotNull(key1);
      Assert.True(key1.TokenName == "ValidToken1");
      key1= registry.VerifiedKey("ValidToken1");
      Assert.NotNull(key1);
      Assert.True(key1.TokenName == "ValidToken1");

      var key2= registry.VerifiedKey("ValidToken2");
      Assert.NotNull(key2);
      Assert.True(key2.TokenName == "ValidToken2");
      key2= registry.VerifiedKey("ValidToken2");
      Assert.NotNull(key2);
      Assert.True(key2.TokenName == "ValidToken2");
      //reset valid until in cached object to before, in data store it will still be correct
      key2.ValidUntil= App.TimeInfo.Now.AddMonths(-1);
      key2= registry.VerifiedKey("ValidToken2");
      Assert.NotNull(key2);
      Assert.True(key2.TokenName == "ValidToken2");

      var key3= registry.VerifiedKey("ExpiredToken1");
      Assert.Null(key3);

      var key4= registry.VerifiedKey("NotYetValidToken1");
      Assert.Null(key4);

      var key5= registry.VerifiedKey("DeletedToken1");
      Assert.Null(key5);

      var key6= registry.VerifiedKey("DeletedToken2");
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
      var token= registry.Register(key, key, key);
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
      var token= registry.Register(key, key, key);
      Assert.NotNull(token);
      Assert.True(token.TokenName == key);
      Assert.True(token.Description == key);

      token= registry.VerifiedKey(key);
      Assert.NotNull(token);
      Assert.True(token.TokenName == key);

      token= registry.Deregister(key);
      Assert.NotNull(token);
      Assert.True(token.TokenName == key);

      token= registry.VerifiedKey(key);
      Assert.Null(token);
    }
  }
}