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
  public class DocSchemaBaseCtrl : Microsoft.AspNetCore.Mvc.Controller {
    private static readonly ILogger<DocSchemaBaseCtrl> log= App.Logger<DocSchemaBaseCtrl>();
    private IDocSchemaRepo repo;
    private ISerializer<DocumentSchema> schemaSeri;
    private IDocProcessorRepo docProcRepo;

    ///<summary>Ctor from <paramref name="repo"/> and <paramref name="schemaSeri"/>.</summary>
    public DocSchemaBaseCtrl(IDocSchemaRepo repo, ISerializer<DocumentSchema> schemaSeri, IDocProcessorRepo docProcRepo) {
      this.repo= repo;
      this.schemaSeri= schemaSeri;
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
          repo.CreateFromStreams<AbstractDocument>(defStreams, docProcRepo);
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
        if (xls_file.Headers["Content-Transfer-Encoding"].ToString().Equals("base64", StringComparison.InvariantCultureIgnoreCase))
          using (var rd = new StreamReader(defStreams.CalcModel))
            defStreams.CalcModel= new MemoryStream(Convert.FromBase64String(rd.ReadToEnd()));
      }
      return defStreams;
    }

    private const string STYLE_DATA= "styles.css";

    ///<summary>Returns <see cref="DocumentSchema"/> form-data based on <paramref name="typeId"/>.</summary>
    ///<remarks>
    ///If the given typeId starts with 'styles.css?doc=' the forms CSS is returned,
    ///otherwise the form HTML is returned.
    ///</remarks>
    //[GET] api/DocSchema/form/{typeId}
    protected void FormIntern(string typeId) {
      if (STYLE_DATA == typeId.ToLowerInvariant()) {
        typeId= this.Request.Query["form"];
        this.Response.ContentType= "text/css; charset=utf-8";
        writeFormResponseData(this.Response, typeId, (s) => s.FormStyleData);
        return;
      }

      this.Response.ContentType= "text/html; charset=utf-8";
      writeFormResponseData(this.Response, typeId, (s) => s.FormData);
    }

    private void writeFormResponseData(HttpResponse resp, string typeId, Func<DocumentSchema, byte[]> selectData) {
      DocumentSchema schema;

      try {
        schema= repo.GetByTypeId(typeId);
        var data= selectData(schema);
        resp.Body.Write(data, 0, data.Length);
      }
      catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException) {
        var err= $"Unknown document type: '{typeId}'";
        log.LogError(0, ex, err);
        resp.StatusCode= 404;
        resp.WriteAsync(err);
      }
    }

  }
}