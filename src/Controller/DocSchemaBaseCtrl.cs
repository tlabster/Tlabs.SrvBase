using System;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Tlabs.Data.Repo;
using Tlabs.Data.Entity;
using Tlabs.Data.Serialize;
using Tlabs.Data.Processing;

namespace Tlabs.Server.Controller {

  ///<summary><see cref="DocumentSchema"/> upload controller.</summary>
  [Route("api/[controller]/[action]")]
  public class DocSchemaBaseCtrl : ApiCtrl {
    private static readonly ILogger<DocSchemaBaseCtrl> log= App.Logger<DocSchemaBaseCtrl>();
    ///<summary>Schema repo..</summary>
    protected IDocSchemaRepo schemaRepo;
    ///<summary>Doc. processor repo..</summary>
    protected IDocProcessorRepo docProcRepo;

    ///<summary>Ctor from <paramref name="repo"/> and <paramref name="docProcRepo"/>.</summary>
    public DocSchemaBaseCtrl(IDocSchemaRepo repo, IDocProcessorRepo docProcRepo) {
      this.schemaRepo= repo;
      this.docProcRepo= docProcRepo;
    }

    ///<summary>POST upload action with <paramref name="xml_file"/>, <paramref name="html_file"/> and <paramref name="css_file"/>.</summary>
    //[POST] api/DocSchema/upload
    protected void UploadIntern(IFormFile html_file, IFormFile xml_file= null, IFormFile css_file= null, IFormFile xls_file= null) {
      var req= this.Request;
      var resp= this.Response;
      if (!req.HasFormContentType) {
        resp.StatusCode= 415;
        return;
      }
      resp.ContentType= "text/plain; charset=utf-8";
      using (var respWr = new StreamWriter(resp.Body, Encoding.UTF8)) {
        try {
          var defStreams= CreateSchemaDefStreams(xml_file, html_file, css_file, xls_file);
          schemaRepo.CreateFromStreams(defStreams, docProcRepo);
        }
        catch (Exception e) {
          log.LogError(0, e, "Schema upload failed.");
          resp.StatusCode= 400;
          var vse= e as Dynamic.ExpressionSyntaxException;
          if (null != vse && null != vse.SyntaxErrors) foreach(var error in vse.SyntaxErrors) {
            respWr.WriteLine(error.Message);
          }
          respWr.WriteLine(vse?.Message ?? e.ToString());
        }
      }
    }

    ///<summary>Create a <see cref="SchemaDefinitionStreams"/> from <see cref="IFormFile"/>(s).</summary>
    public static SchemaDefinitionStreams CreateSchemaDefStreams(IFormFile xml_file, IFormFile html_file= null, IFormFile css_file= null, IFormFile xls_file= null) {
      var defStreams= new SchemaDefinitionStreams {
        Schema= xml_file.OpenReadStream(),
        CalcModel= xls_file?.OpenReadStream(),
        Form= html_file?.OpenReadStream(),
        Style= css_file?.OpenReadStream()
      };

      if (null != defStreams.CalcModel) {
        //check for possible base64 encoding
        if (xls_file.Headers["Content-Transfer-Encoding"].ToString().Equals("base64", StringComparison.OrdinalIgnoreCase))
          using (var rd = new StreamReader(defStreams.CalcModel))
            defStreams.CalcModel= new MemoryStream(Convert.FromBase64String(rd.ReadToEnd()));
      }
      return defStreams;
    }

    private const string STYLE_DATA= "styles.css";

    ///<summary>Returns <see cref="DocumentSchema"/> form-data based on <paramref name="typeId"/>.</summary>
    ///<remarks>
    ///If the given typeId starts with 'styles.css?form=' the forms CSS is returned,
    ///otherwise the form HTML is returned.
    ///</remarks>
    //[GET] api/DocSchema/form/{typeId}
    [Obsolete("Use formIntern method with one callback parameter.", false)]
    protected void FormIntern(string typeId, string form, Func<string, Stream> formDataStream, Func<string, Stream> styleDataStream) {
      var resp= this.Response;
      try {
        if (STYLE_DATA == typeId.ToLowerInvariant()) {
          typeId= form;
          resp.ContentType= "text/css; charset=utf-8";
          styleDataStream(typeId).CopyTo(resp.Body);
          return;
        }

        resp.ContentType= "text/html; charset=utf-8";
        formDataStream(typeId).CopyTo(resp.Body);
      }
      catch (Exception e) {
        this.resolveError(e);
      }
    }

    ///<summary>Returns <see cref="DocumentSchema"/> form-data based on <paramref name="typeId"/>.</summary>
    ///<remarks>
    ///If the given typeId starts with 'styles.css?form=' the forms CSS is returned,
    ///otherwise the form HTML is returned.
    ///</remarks>
    //[GET] api/DocSchema/form/{typeId}
    protected void FormIntern(string typeId, string form, Func<string, SchemaDefinitionStreams.Data, Stream> schemaStream) {
      var resp= this.Response;
      try {
        if (STYLE_DATA == typeId.ToLowerInvariant()) {
          typeId= form;
          resp.ContentType= "text/css; charset=utf-8";
          schemaStream(typeId, SchemaDefinitionStreams.Data.Style).CopyTo(resp.Body);
          return;
        }

        resp.ContentType= "text/html; charset=utf-8";
        schemaStream(typeId, SchemaDefinitionStreams.Data.Markup).CopyTo(resp.Body);
      }
      catch (Exception e) {
        this.resolveError(e);
      }
    }
  }
}