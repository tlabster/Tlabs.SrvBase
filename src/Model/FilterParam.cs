﻿using System;
using System.Collections.Generic;
using System.Linq;

using Tlabs.Data.Model;
using Tlabs.Data.Serialize;
using Tlabs.Data.Serialize.Json;

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

  }

  ///<summary>Filter parameter model to be bound via MVC model binding.</summary>
  ///<remarks>This filter parameter model is aimed to bind a request like:
  /// <code>api/sampleTypes?_dc=1020304050607&amp;start=0&amp;limit=25&amp;filter=[{"isFormFilter":true,"anyMatch":true,"disableOnEmpty":true,"property":"lastname","value":"aal","operator":"like"}]</code>
  ///<para>See MVC model binding: https://docs.microsoft.com/en-us/aspnet/core/mvc/models/model-binding</para>
  /// NOTE:<para>
  /// Unfortunately MVC's model binding requires that the class must have a public default constructor and thus does not support injecting dependencies through DI...<br/>
  /// Furthermore complex type properties are *NOT* being bound using JSON deserialization.
  ///</para>
  ///</remarks>
  public class FilterParam<TEntity> : PagingParam {
    static readonly IDynamicSerializer JSON= JsonFormat.CreateDynSerializer();
    ///<summary>Filter list.</summary>
    private string filterStr;
    private string sorterStr;
    ///<summary>Sorter list.</summary>

    ///<summary>Default ctor.</summary>
    public FilterParam() {
      this.FilterList= Enumerable.Empty<Filter>();
      this.SorterList= Enumerable.Empty<Sorter>();
    }
    ///<summary>filter string with format: <c>[{"property":"lastname","value":"aal","operator":"like"}]</c>.</summary>
    public string filter {
      get => filterStr;
      set => this.FilterList= (IList<Filter>)JSON.LoadObj(filterStr= value, typeof(IList<Filter>));
    }

    ///<summary>List of <see cref="Filter"/>(s).</summary>
    public IEnumerable<Filter> FilterList { get; set; }

    ///<summary>sort string with format: <c>[{"property":"fieldName","direction":"ASC"}]</c>.</summary>
    public string sort {
      get { return sorterStr; }
      set => this.SorterList= (IList<Sorter>)JSON.LoadObj(sorterStr= value, typeof(IList<Sorter>));
    }

    ///<summary>List of <see cref="Sorter"/>(s).</summary>
    public IEnumerable<Sorter> SorterList { get; set;}

    /// <summary>Filters that are enforced</summary>
    public Dictionary<string, string> EnforcedFilters { get; set; }

    /// <summary>Return <see cref="QueryFilter"/> from this filter parameter(s).</summary>
    public QueryFilter AsQueryFilter()
      => new QueryFilter {
        Start= this.start,
        Limit= this.limit,
        Properties= this.FilterList.ToDictionary(f => f.property, f => (IConvertible)f.value),
        SortAscBy= this.SorterList.ToDictionary(s => s.property, s => s.IsAscSort())
      };

  }

  ///<summary>Filter descriptor.</summary>
  public class Filter {
    ///<summary>Field/property name.</summary>
    public string property { get; set; }
    ///<summary>Value to compare.</summary>
    public string value { get; set; }
    ///<summary>Compare operator.</summary>
    public string @operator { get; set; }
  }

  ///<summary>Sorter descriptor.</summary>
  public class Sorter {
    ///<summary>Value for sort direction ascending</summary>
    public const string ASC= "ASC";
    ///<summary>Field/property name.</summary>
    public string property { get; set; }
    ///<summary>Sort direction.</summary>
    public string direction { get; set; }

    ///<summary>Check for ASC sort direction.</summary>
    public bool IsAscSort() => string.IsNullOrEmpty(this.direction) || string.Equals(ASC, this.direction, StringComparison.OrdinalIgnoreCase);
  }

}
