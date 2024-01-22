using System.Collections.Generic;
using System.Linq;

using Xunit;
using Xunit.Abstractions;

namespace Tlabs.Server.Model.Test {

  public class FilterParamTest {
    ITestOutputHelper tstout;
    public FilterParamTest(ITestOutputHelper tstout) => this.tstout= tstout;

    private class TestModel {
      public string param { get; set; }
    }

    [Fact]
    public void BasicTest() {

      var filter= new FilterParam<TestModel> { };
      Assert.Equal(0, filter.start);
      Assert.Equal(50, filter.limit);
      Assert.Null(filter.FilterList);
      Assert.Null(filter.SorterList);

      filter= new() {
        filter= "",
        sort= "[]"
      };
      Assert.Equal("", filter.filter);
      Assert.Null(filter.FilterList);
      Assert.Empty(filter.SorterList);

      filter= new() {
        filter= "[{\"property\":\"Name\",\"value\":\"x\"}, {\"property\":\"Num\",\"value\": \"1\"}]",
        sort= "[{\"property\":\"Name\",\"direction\":\"aSc\"}]"
      };
      Assert.NotEmpty(filter.FilterList);
      Assert.Single(filter.SorterList);
      Assert.Equal("x", filter.FilterList.First().value);
      Assert.True(filter.SorterList.Single().IsAscSort());

      var queryFilter= filter.AsQueryFilter();
      Assert.NotEmpty(queryFilter.Properties);
      Assert.Equal("x", queryFilter.Properties["Name"]);
      Assert.True(queryFilter.SortAscBy["Name"]);
      Assert.Equal(1, queryFilter.Properties["Num"].ToInt32(System.Globalization.NumberFormatInfo.InvariantInfo));
      Assert.Throws<KeyNotFoundException>(()=>queryFilter.Properties["undefined"]);
    }
  }
}