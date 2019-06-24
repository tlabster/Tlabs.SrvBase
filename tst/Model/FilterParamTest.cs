using System.IO;
using System.Collections.Generic;
using System.Text;

using Tlabs.Data.Serialize.Json;

using Xunit;
using Xunit.Abstractions;

using Tlabs.Config;

namespace Tlabs.Server.Model.Test {

  public class FilterParamTest {

    private class TestModel {
      public string param { get; set; }
    }

    [Fact]
    public void BasicTest() {

      var filter= new FilterParam<TestModel> {
        filter= "[]",
        sort= "[]"
      };
      Assert.Equal(0, filter.start);
      Assert.Equal(50, filter.limit);
      Assert.NotNull(filter.FilterList);
      Assert.NotNull(filter.SorterList);
    }

    static string FPSTR= @"
{""filter"":""[{\""property\"":\""Name\"",\""value\"":\""P\""}]""}
";
    [Fact]
    public void JsonTest() {
      var jsonSeri= JsonFormat.CreateSerializer<FilterParam<TestModel>>();
      var strm= new MemoryStream(Encoding.UTF8.GetBytes(FPSTR));
      var fparam= jsonSeri.LoadObj(strm);
      Assert.NotEmpty(fparam.FilterList);
    }

  }
}