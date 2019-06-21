using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Linq;

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
      get { return st ?? DEFAULT_START; }
      set { st= value; }
    }

    ///<summary>Page size.</summary>
    public int? limit {
      get { return lim ?? DEFAULT_LIMIT; }
      set { lim= value; }
    }

  }

  ///<summary>Delegate function to add a filter to <c>IQueryable&lt;T&gt;</c>.</summary>
  public delegate IQueryable<T> FilterExpression<T>(IQueryable<T> q, FilterParam<T>.Filter f);
  ///<summary>Delegate function to add a soter to <c>IQueryable&lt;T&gt;</c>.</summary>
  public delegate IQueryable<T> SorterExpression<T>(IQueryable<T> q, FilterParam<T>.Sorter s);

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
    private static readonly IDynamicSerializer JSON= JsonFormat.CreateDynSerializer();

    private string filterStr;
    ///<summary>Filter list.</summary>
    protected IList<Filter> filterList;
    private string sorterStr;
    ///<summary>Sorter list.</summary>
    protected IList<Sorter> sorterList;

    ///<summary>filter string with format: <c>[{"property":"lastname","value":"aal","operator":"like"}]</c>.</summary>
    public string filter {
      get => filterStr;
      set => this.filterList= (IList<Filter>)JSON.LoadObj(filterStr= value, typeof(IList<Filter>));
    }

    ///<summary>List of <see cref="Filter"/>(s).</summary>
    public IList<Filter> FilterList => filterList;

    ///<summary>sort string with format: <c>[{"property":"fieldName","direction":"ASC"}]</c>.</summary>
    public string sort {
      get { return sorterStr; }
      set => this.sorterList= (IList<Sorter>)JSON.LoadObj(sorterStr= value, typeof(IList<Sorter>));
    }

    ///<summary>List of <see cref="Sorter"/>(s).</summary>
    public IList<Sorter> SorterList  => sorterList;

    /// <summary>Filters that are enforced</summary>
    public Dictionary<string, string> EnforcedFilters { get; set; }
    
    ///<summary>Apply this filter parameters to the <paramref name="query"/>.</summary>
    public IQueryable<TEntity> ApplyFilter(IQueryable<TEntity> query,
                                           IDictionary<string, FilterExpression<TEntity>> filterMap,
                                           IDictionary<string, SorterExpression<TEntity>> sorterMap) {

      if (null != filterList) foreach (var f in filterList) { //apply filter(s) to query
        FilterExpression<TEntity> filter;
        if (filterMap.TryGetValue(f.property, out filter))
          query= filter(query, f);
      }

      if (null != EnforcedFilters) foreach (var f in EnforcedFilters) { //apply filter(s) to query
        FilterExpression<TEntity> filter;
        if (filterMap.TryGetValue(f.Key, out filter)) {
            query= filter(query, new Filter {property= f.Key, value= f.Value});
        }
      }

      if (null != sorterList) foreach (var s in sorterList) { // apply sorter(s) to query
        SorterExpression<TEntity> sorter;
        if (sorterMap.TryGetValue(s.property, out sorter))
          query= sorter(query, s);
      }
      return query;                                        
    }

    ///<summary>Filter descriptor.</summary>
    public class Filter {
      ///<summary>Field/property name.</summary>
      public string property { get; set; }
      ///<summary>Value to compare.</summary>
      public IConvertible value { get; set; }
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
      public bool IsAscSort() => 0 == string.Compare(ASC, this.direction, StringComparison.OrdinalIgnoreCase);

      ///<summary>Add sorter by <paramref name="prop">property selector</paramref> (of type <typeparamref name="P"/>) to <paramref name="query"/>.</summary>
      public IQueryable<T> Add<T, P>(IQueryable<T> query, System.Linq.Expressions.Expression<Func<T, P>> prop) {
        return IsAscSort() ? query.OrderBy(prop) : query.OrderByDescending(prop);
      }
    }
  }

}
