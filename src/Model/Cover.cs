using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

using Microsoft.Extensions.Logging;

namespace Tlabs.Server.Model {

  ///<summary>Abstract cover for returned model objects.</summary>
  public abstract class AbstractCover<T> {
    ///<summary>Logger.</summary>
    protected static readonly ILogger log= App.Logger<AbstractCover<T>>();

    ///<summary>True if requested model could be successfully returned.</summary>
    public bool success {
      get { return string.IsNullOrEmpty(error); }
    }
    ///<summary>Any description of an error causing the model retrieval to fail.</summary>
    public string error { get; set; }

    ///<summary>Handle exception.</summary>
    protected virtual void handleException(Exception e, Func<Exception, string> provideErrMessage) {
      this.error= provideErrMessage?.Invoke(e);
      if (null == this.error) {
        this.error= $"Failed to return {typeof(T).Name}: ({e.Message}).";
        log.LogError(0, e, this.error);
      }
    }
  }

  ///<summary>Cover for a single model object being provided from delegate.</summary>
  public class ModelCover<M> : AbstractCover<M> {
    ///<summary>Default ctor.</summary>
    protected ModelCover() { }
    ///<summary>Ctor from <paramref name="provideModel"/> and (optional) <paramref name="provideErrMessage"/> delegates.</summary>
    ///<remarks>Use with a controller like:
    ///<code>
    ///[HttpPut]
    ///public ModelCover&lt;MyModel&gt; Update([FromBody]MyModel model) {
    ///  return new ModelCover&lt;MyModel&gt;(() => {
    ///    /* do anything to obtain the model */
    ///    return model;
    ///  });
    ///}
    ///</code>
    ///</remarks>
    public ModelCover(Func<ModelCover<M>, M> provideModel, Func<Exception, string> provideErrMessage= null) {
      try {
        this.data= provideModel(this);
      }
      catch (Exception e) {
        handleException(e, provideErrMessage);
      }
    }
    ///<summary>The (covered) model object.</summary>
    public M data { get; protected set; }
  }

  ///<summary>Cover for the result of a data query for model objects.</summary>
  public class QueryCover<M> : AbstractCover<M> {
    ///<summary>Default ctor called from derived class ctors.</summary>
    protected QueryCover() { }
    ///<summary>Ctor from <paramref name="queryResult"/> and (optional) <paramref name="provideErrMessage"/> delegates.</summary>
    public QueryCover(Func<QueryCover<M>, IEnumerable<M>> queryResult, Func<Exception, string> provideErrMessage= null) {
      try {
        this.data= queryResult(this);
      }
      catch (Exception e) {
        handleException(e, provideErrMessage);
      }
    }
    ///<summary>The (covered) result of the query as enumeration.</summary>
    public virtual IEnumerable<M> data { get; protected set; }
  }


  ///<summary>Cover for the result of a <see cref="IQueryable{T}"/> returned as a projected <see cref="IEnumerable{M}"/>.</summary>
  public class QueryCover<T, M> : QueryCover<M> {
    ///<summary>Default ctor called from derived class ctors.</summary>
    protected QueryCover() { }
    ///<summary>Ctor from <paramref name="query"/> and <paramref name="selector"/>.</summary>
    public QueryCover(IQueryable<T> query, Expression<Func<T, M>> selector) {
      var p= new QueryProjector<T, M>(query, selector);
      execQuery(p);
    }

    /* Note:
     * This deliberately is not being virtual to allow for calling this safely from any ctor.
     * (Any 'overriding' enhancements to the query are left to be implemented with a QueryProjecto<T,M>...)
     */
    ///<summary>Executes the actual query and sets the (covered) result data.</summary>
    protected void execQuery(QueryProjector<T, M> qp) {
      try {
        this.data= qp.Projection().ToList();
      }
      catch (Exception e) {
        this.error= $"Failed to return {typeof(M).Name}(s): ({e.Message}).";
        log.LogError(0, e, this.error);
      }
    }

    ///<summary>Class to encapsulate the projection from source <see cref="IQueryable{T1}"/> to destination <see cref="IQueryable{M1}"/> plus any (optional) query enhancements.</summary>
    protected class QueryProjector<T1, M1> {
      ///<summary>Source query of type T1</summary>
      public IQueryable<T1> query;
      ///<summary>Selector expression from T1 to M1</summary>
      protected Expression<Func<T1, M1>> selector;
      
      ///<summary>Ctor from <paramref name="query"/> and <paramref name="selector"/>.</summary>
      public QueryProjector(IQueryable<T1> query, Expression<Func<T1, M1>> selector) {
        this.query= query;
        this.selector= selector;
      }

      ///<summary>Returns the projection to <see cref="IQueryable{M1}"/>.</summary>
      ///<remarks>subclasses could override this implementation to also enhance the query.</remarks>
      public virtual IQueryable<M1> Projection() {
        return query.Select(selector);  //simply project to model type M
      }
    }
  }

  ///<summary>Cover for the concatenated result(s) of <see cref="IQueryable{T}"/>(s).</summary>
  public class ConcatQueryCover<T> : AbstractCover<T> {
    private IEnumerable<T> dataEnum;

    ///<summary>Ctor from multiple <paramref name="queries"/>(s).</summary>
    public ConcatQueryCover(params IQueryable<T>[] queries) {
      foreach (var q in queries) {
        var list= q.ToList();
        dataEnum= dataEnum?.Concat(list) ?? list;
      }
    }
    ///<summary>The (covered) concatenated result of the query(s) as enumeration.</summary>
    public IEnumerable<T> data { get { return dataEnum; } }

  }

  ///<summary>Cover for the result of a a page limited data query for model objects.</summary>
  public class PagedQueryCover<M> : QueryCover<M> {
    ///<summary>Ctor from <paramref name="queryResult"/> and (optional) <paramref name="provideErrMessage"/> delegates.</summary>
    public PagedQueryCover(Func<PagedQueryCover<M>, Data.Model.IResultList<M>> queryResult, Func<Exception, string> provideErrMessage = null) {
      try {
        var res= queryResult(this);
        this.total= res.Total;
        this.data= res.Data;
      }
      catch (Exception e) {
        handleException(e, provideErrMessage);
      }
    }

    ///<summary>Total (unlimited) result count.</summary>
    public int total { get; set; }
  }

  ///<summary>Cover for the result of a <see cref="IQueryable{T}"/> returned as a page limited projection into <see cref="IEnumerable{M}"/>.</summary>
  public class PagedQueryCover<T, M> : QueryCover<T, M> {
    ///<summary>Paging parameters.</summary>
    protected PagingParam pageParam;

    ///<summary>Ctor from <paramref name="query"/>, <paramref name="pageParam"/> and <paramref name="selector"/>.</summary>
    public PagedQueryCover(IQueryable<T> query, PagingParam pageParam, Expression<Func<T, M>> selector) {
      this.pageParam= pageParam;
      var p= new PageProjector<T, M>(query, selector, this);
      execQuery(p);
    }
    
    ///<summary>Total (unlimited) result count.</summary>
    public int total { get; protected set; }

    ///<summary>Paging specific QueryProjector.</summary>
    protected class PageProjector<T1, M2> : QueryProjector<T1, M2> {
      ///<summary>Parent cover.</summary>
      protected PagedQueryCover<T1, M2> pagedCover;

      ///<summary>Ctor from <paramref name="query"/>, <paramref name="selector"/> and <paramref name="pagedCover"/>.</summary>
      public PageProjector(IQueryable<T1> query, Expression<Func<T1, M2>> selector, PagedQueryCover<T1, M2> pagedCover) : base(query, selector) {
        this.pagedCover= pagedCover;
      }

      ///<summary>Paging specific query projection.</summary>
      public override IQueryable<M2> Projection() {
        var query0= query;
        var page= pagedCover.pageParam;

        if (page.start.HasValue)
          query= query.Skip(pagedCover.pageParam.start.Value);
        if (page.limit.HasValue)
          query= query.Take(pagedCover.pageParam.limit.Value);
#if GROUPED_COUNT_QUERY
        query= pagedCover.query.GroupBy(e => new { Total= query0.Count() }).First();
        pagedCover.total= query.Key.Total;
#else
        pagedCover.total= query0.Count();
#endif
        return base.Projection();
      }
    }

  }
}