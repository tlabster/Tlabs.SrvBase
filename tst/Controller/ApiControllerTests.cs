using System;
using Microsoft.AspNetCore.Http;

using Xunit;


namespace Tlabs.Server.Controller.Test {

  public class ApiControllerTests {

    [Fact]
    public void ResolveErrorTest() {
      var cntrl= new TestApiCtrl(new Exception());
      Assert.Contains("internal state", cntrl.ResolvedMsg);
      Assert.Equal(StatusCodes.Status500InternalServerError, cntrl.ResolvedCode);

      cntrl= new TestApiCtrl(new ArgumentException("missing arg"));
      Assert.Contains("missing", cntrl.ResolvedMsg);
      Assert.Equal(StatusCodes.Status400BadRequest, cntrl.ResolvedCode);

      cntrl= new TestApiCtrl(new GeneralException("Test MSG", new InvalidOperationException("invalid msg contains no elements")));
      Assert.Equal("Test MSG", cntrl.ResolvedMsg);
      Assert.Equal(StatusCodes.Status404NotFound, cntrl.ResolvedCode);

    }

  }

  class TestApiCtrl : ApiCtrl {
    public string ResolvedMsg { get; }
    public int? ResolvedCode => this.ResolvedStatusCode;
    public TestApiCtrl(Exception e, string msg= null) {
      this.ResolvedMsg= this.resolveError(e, msg);
    }
  }
}