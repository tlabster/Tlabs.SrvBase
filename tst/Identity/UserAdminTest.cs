
using System;
using System.Linq;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

using Tlabs.Misc;
using Tlabs.Data;
using Moq;
using Xunit;
using System.Diagnostics.CodeAnalysis;

namespace Tlabs.Identity.Intern.Test {

  public class UserAdminTest : IClassFixture<UserAdminTest.Fixture> {
    public class Fixture {
      public readonly IList<Data.Entity.User> TstUsers= new List<Data.Entity.User> {
        new Data.Entity.User {
          Id= 1,
          UserName= "usr01",
          NormalizedUserName= "usr01",
          Email= "usr01@mailinator.com",
          NormalizedEmail= "usr01@mailinator.com",
          Status= "ACTIVE"
        },
        new Data.Entity.User {
          Id= 2,
          UserName= "usr02",
          NormalizedUserName= "usr02",
          Email= "usr02@mailinator.com",
          NormalizedEmail= "usr02@mailinator.com",
          Status= "ACTIVE"
        }

      };

      public readonly IUserAdministration UserAdm;
      public Fixture() {
        var authSvcMoq= new Mock<IAuthenticationService>();
        authSvcMoq.Setup(a => a.SignInAsync(It.IsAny<HttpContext>(), It.IsAny<string>(), It.IsAny<ClaimsPrincipal>(), It.IsAny<AuthenticationProperties>()))
                  .Returns(Task.CompletedTask);
        // AuthenticationTicket atck= null;
        // authSvcMoq.Setup(a => a.AuthenticateAsync(It.IsAny<HttpContext>(), It.IsAny<string>()))
        //           .Callback()
        //           .ReturnsAsync(atck);
        authSvcMoq.Setup(a => a.SignOutAsync(It.IsAny<HttpContext>(), It.IsAny<string>(), It.IsAny<AuthenticationProperties>()))
                  .Returns(Task.CompletedTask);
        var authSvc= authSvcMoq.Object;

        var svcProvMoq= new Mock<IServiceProvider>();
        svcProvMoq.Setup(p => p.GetService(It.Is<Type>(t => typeof(IAuthenticationService).IsAssignableFrom(t))))
              .Returns(authSvc);
        var svcProv= svcProvMoq.Object;

        var httpCtxMoq= new Mock<HttpContext>();
        httpCtxMoq.Setup(h => h.RequestServices)
                  .Returns(svcProv);
        var httpCtx= httpCtxMoq.Object;

        var ctxAccMoq= new Mock<IHttpContextAccessor>();
        ctxAccMoq.Setup(c => c.HttpContext)
              .Returns(httpCtx);
        var ctxAcc= ctxAccMoq.Object;

        var optMoq= new Mock<IOptions<IdentityOptions>>() { DefaultValue= DefaultValue.Mock };
        optMoq.Setup(o => o.Value).Returns(new IdentityOptions {  });
        var identOptions= optMoq.Object;

        var norm= new Normalizer();

        var usrMngrMoq= new Mock<UserManager<Data.Entity.User>> (
          (new Mock<IUserStore<Data.Entity.User>> { DefaultValue= DefaultValue.Mock }).Object,
          identOptions,
          (new Mock<IPasswordHasher<Data.Entity.User>> { DefaultValue= DefaultValue.Mock }).Object,
          Enumerable.Empty<IUserValidator<Data.Entity.User>>(),
          Enumerable.Empty<IPasswordValidator<Data.Entity.User>>(),
          norm,
          (new Mock<IdentityErrorDescriber> { DefaultValue= DefaultValue.Mock }).Object,
          svcProv,
          Tlabs.App.Logger<UserManager<Data.Entity.User>>()
        ) { DefaultValue = DefaultValue.Mock };
        usrMngrMoq.Setup(u => u.Users)
                  .Returns(TstUsers.AsQueryable());
        var usrMngr= usrMngrMoq.Object;

        var sgnMngrMoq= new Mock<SignInManager<Data.Entity.User>>(
          usrMngr,
          ctxAcc,
          (new Mock<IUserClaimsPrincipalFactory<Data.Entity.User>> { DefaultValue= DefaultValue.Mock }).Object,
          identOptions,
          Tlabs.App.Logger<SignInManager<Data.Entity.User>>(),
          (new Mock<IAuthenticationSchemeProvider> { DefaultValue= DefaultValue.Mock }).Object,
          (new Mock<IUserConfirmation<Data.Entity.User>> { DefaultValue= DefaultValue.Mock }).Object
        ) { DefaultValue = DefaultValue.Mock };
        var sgnMngr= sgnMngrMoq.Object;

        var store= new NoopStoreConfigurator.NoopDataStore();
        var locRepoMoq= new Mock<ICachedRepo<Tlabs.Data.Entity.Locale>>();
        locRepoMoq.Setup(repo => repo.Store)
                  .Returns(store);
        var locRepo= locRepoMoq.Object;

        this.UserAdm= new UserAdministration(ctxAcc, usrMngr, sgnMngr, locRepo, identOptions);
      }
    }

    private class Normalizer : ILookupNormalizer {
      [return: NotNullIfNotNull("email")]
      public string NormalizeEmail(string email) => email;

      [return: NotNullIfNotNull("name")]
      public string NormalizeName(string name) => name;
    }

    Fixture fix;

    public UserAdminTest(Fixture fix) {
      this.fix= fix;
    }

    [Fact]
    public async Task BasicTest() {
      var usrAdm= fix.UserAdm;

      var resLst= usrAdm.FilteredList();
      Assert.NotNull(resLst);
      Assert.NotEmpty(resLst.Data);

      Assert.NotNull(usrAdm.GetByEmail("usr02@mailinator.com"));
      Assert.Throws<DataEntityNotFoundException<Data.Entity.User>>(() => usrAdm.GetByEmail("undefined"));

      await Assert.ThrowsAnyAsync<ArgumentException>(() => usrAdm.Login("usr01", ""));
      var res= await usrAdm.Login("usr01", "invalid");
      Assert.Equal(LoginResult.FAILED, res);
    }
  }
}
