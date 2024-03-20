using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;

using Tlabs.Data.Model;
using Tlabs.Data.Serialize.Json;
using static Tlabs.Data.Serialize.Json.JsonFormat;

namespace Tlabs.Server.Model {
  ///<summary>Paging (query) parameter model to be bound via MVC model binding.</summary>
  public class PagingParam {
    ///<summary>Default page start index.</summary>
    public const int DEFAULT_START= 0;
    ///<summary>Default page size.</summary>
    public const int DEFAULT_LIMIT= 50;
    private int? st;
    private int? lim;

    ///<summary>Page number.</summary>
    public int? page { get; set; }

    ///<summary>Page start index.</summary>
    public int? start {
      get => st ?? DEFAULT_START;
      set => st= value;
    }

    ///<summary>Page size.</summary>
    public int? limit {
      get => lim ?? DEFAULT_LIMIT;
      set => lim= value;
    }

    /// <summary>Support custom binding.</summary>
    public static ValueTask<PagingParam?> BindAsync(HttpContext httpCtx) {
      PagingParam? pagingParam= null;
      pagingParam= new() {
        limit=   int.TryParse(httpCtx.Request.Query[nameof(pagingParam.limit)], App.DfltFormat, out var limit)
               ? limit
               : null,
        page=    int.TryParse(httpCtx.Request.Query[nameof(pagingParam.page)], App.DfltFormat, out var page)
               ? page
               : null,
        start=   int.TryParse(httpCtx.Request.Query[nameof(pagingParam.start)], App.DfltFormat, out var start)
               ? start
               : null,
      };
      return ValueTask.FromResult<PagingParam?>(pagingParam);
    }
  }

  ///<summary>Filter parameter model to be bound via MVC model binding.</summary>
  ///<remarks>This filter parameter model is aimed to bind a request like:
  /// <code>api/sampleTypes?start=0&amp;limit=25&amp;filter=[{"property":"lastname","value":"xyz"}]</code>
  /// NOTE:<para>
  /// This binds the <see cref="FilterParam{T}.filter"/> to an array of <see cref="Filter"/> objects and
  /// <see cref="FilterParam{T}.sort"/> to an array of <see cref="Sorter"/> objects respectively.
  ///</para>
  ///</remarks>
  public class FilterParam<TEntity> : PagingParam {
    static readonly DynamicSerializer JSON= JsonFormat.CreateDynSerializer();
    ///<summary>Filter list.</summary>
    private string? filterStr;
    private string? sorterStr;

    ///<summary>filter string with format: <c>[{"property":"lastname","value":"aal","operator":"like"}]</c>.</summary>
    public string? filter {
      get => filterStr;
      set => this.FilterList=   !string.IsNullOrWhiteSpace(filterStr= value)
                              ? JSON.LoadObj(filterStr, typeof(IList<Filter>)) as IEnumerable<Filter>
                              : null;
    }

    ///<summary>List of <see cref="Filter"/>(s).</summary>
    public IEnumerable<Filter>? FilterList { get; set; }

    ///<summary>sort string with format: <c>[{"property":"fieldName","direction":"ASC"}]</c>.</summary>
    public string? sort {
      get { return sorterStr; }
      set => this.SorterList=   !string.IsNullOrWhiteSpace(sorterStr= value)
                              ? JSON.LoadObj(sorterStr, typeof(IList<Sorter>)) as IEnumerable<Sorter>
                              : null;
    }

    ///<summary>List of <see cref="Sorter"/>(s).</summary>
    public IEnumerable<Sorter>? SorterList { get; set;}

    /// <summary>Filters that are enforced</summary>
    [Obsolete("This is no longer recognized - enforced access filters are applied with RoleDefaultParamsFilter ", false)]
    public Dictionary<string, string>? EnforcedFilters { get; set; }

    /// <summary>Return <see cref="QueryFilter"/> from this filter parameter(s).</summary>
    public QueryFilter AsQueryFilter()
      => new QueryFilter {
        Start= this.start,
        Limit= this.limit,
        Properties= this.FilterList?.ToDictionary(f => f.property ?? "?", f => f.value as IConvertible)!,
        SortAscBy= this.SorterList?.ToDictionary(s => s.property ?? "?", s => s.IsAscSort())!
      };

    /// <summary>Support custom binding.</summary>
    public static new ValueTask<FilterParam<TEntity>?> BindAsync(HttpContext httpCtx) {
      FilterParam<TEntity>? filterParam= null;

      var pgValTsk= PagingParam.BindAsync(httpCtx);
      if (pgValTsk.IsCompleted) {
        var pagingParam= pgValTsk.Result ?? new();

        filterParam= new() {
          limit= pagingParam.limit,
          page=  pagingParam.page,
          start= pagingParam.start,
          filter= httpCtx.Request.Query[nameof(filterParam.filter)],
          sort=   httpCtx.Request.Query[nameof(filterParam.sort)]
        };
      }
      return ValueTask.FromResult<FilterParam<TEntity>?>(filterParam);
    }
  }

  ///<summary>Filter descriptor.</summary>
  public class Filter {
    ///<summary>Field/property name.</summary>
    public string? property { get; set; }
    ///<summary>Value to compare.</summary>
    public string? value { get; set; }
    ///<summary>Compare operator.</summary>
    [Obsolete("This gets handled with the filterMap of a entity repo.", error: false)]
    public string? @operator { get; set; }
  }

  ///<summary>Sorter descriptor.</summary>
  public class Sorter {
    ///<summary>Value for sort direction ascending</summary>
    public const string ASC= "ASC";
    ///<summary>Field/property name.</summary>
    public string? property { get; set; }
    ///<summary>Sort direction.</summary>
    public string? direction { get; set; }

    ///<summary>Check for ASC sort direction.</summary>
    public bool IsAscSort() => string.IsNullOrEmpty(this.direction) || string.Equals(ASC, this.direction, StringComparison.OrdinalIgnoreCase);
  }

}
