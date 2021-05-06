
using System;
using System.Linq;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

using Tlabs.Data;
using Moq;
using Xunit;

namespace Tlabs.Identity.Intern.Test {

  public class UserAdminTest : IClassFixture<UserAdminTest.Fixture> {
    public class Fixture {
      public readonly IList<Data.Entity.User> TstUsers= new List<Data.Entity.User> {
        new Data.Entity.User {
          Id= 1,
          UserName= "usr01",
          Email= "usr01@mailinator.com",
          Status= "ACTIVE"
        },
        new Data.Entity.User {
          Id= 2,
          UserName= "usr02",
          Email= "usr02@mailinator.com",
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

        var usrMngrMoq= new Mock<UserManager<Data.Entity.User>>();    //TODO: Need to mock all ctor parameters - is this woth the effort???
        usrMngrMoq.Setup(u => u.Users)
                  .Returns(TstUsers.AsQueryable());
        var usrMngr= usrMngrMoq.Object;

        var sgnMngrMoq= new Mock<SignInManager<Data.Entity.User>>();
        var sgnMngr= sgnMngrMoq.Object;

        var locRepoMoq= new Mock<ICachedRepo<Tlabs.Data.Entity.Locale>>();
        var locRepo= locRepoMoq.Object;

        var optMoq= new Mock<IOptions<IdentityOptions>>();
        optMoq.Setup(o => o.Value).Returns(new IdentityOptions {
        });
        var opt= optMoq.Object;
        this.UserAdm= new UserAdministration(ctxAcc, usrMngr, sgnMngr, locRepo, opt);
      }
    }

    Fixture fix;

    public UserAdminTest(Fixture fix) {
      this.fix= fix;
    }

    // [Fact] //UserManager mock not available....
    public void UserListTest() {
      var usrAdm= fix.UserAdm;

      var res= usrAdm.FilteredList();
      Assert.NotNull(res);
      Assert.NotEmpty(res.Data);
    }
  }
}
