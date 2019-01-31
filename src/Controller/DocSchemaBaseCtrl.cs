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
    private IDocSchemaRepo repo;
    private ILogger<DocSchemaBaseCtrl> log;
    private ISerializer<DocumentSchema> schemaSeri;
    private IDocProcessorRepo docProcRepo;

    ///<summary>Ctor from <paramref name="repo"/>, <paramref name="log"/> and <paramref name="schemaSeri"/>.</summary>
    public DocSchemaBaseCtrl(IDocSchemaRepo repo, ILogger<DocSchemaBaseCtrl> log, ISerializer<DocumentSchema> schemaSeri, IDocProcessorRepo docProcRepo) {
      this.repo= repo;
      this.log= log;
      this.schemaSeri= schemaSeri;
      this.docProcRepo= docProcRepo;
    }

    ///<summary>POST upload action with <paramref name="html_file"/>, <paramref name="xml_file"/> and <paramref name="css_file"/>.</summary>
    //[POST] api/DocSchema/upload
    protected void UploadIntern(IFormFile html_file, IFormFile xml_file, IFormFile css_file) {
      var req= this.Request;
      var resp= this.Response;
      if (!req.HasFormContentType) {
        resp.StatusCode= 415;
        return;
      }
      resp.ContentType= "text/plain; charset=utf-8";
      using (var respWr = new StreamWriter(resp.Body, Encoding.UTF8)) {
        try {
          var files= req.Form.Files;
          var xls_file= files.GetFile("xls_file");  //optional xls_file
#if DEBUG
          foreach (var file in files) {
            var msg= $"uploaded file: '{file.Name}' ({file.FileName}) {file.Length/1024}kB";
            log.LogInformation(msg);
            respWr.WriteLine(msg);
          }
#endif
          var schema= schemaSeri.LoadObj(xml_file.OpenReadStream());

          using(var bin= new BinaryReader(html_file.OpenReadStream()))
            schema.FormData= bin.ReadBytes((int)html_file.Length);
          using (var bin= new BinaryReader(css_file.OpenReadStream()))
            schema.FormStyleData= bin.ReadBytes((int)css_file.Length);
          if (null != xls_file) {
            if (xls_file.Headers["Content-Transfer-Encoding"].ToString().Equals("base64", StringComparison.InvariantCultureIgnoreCase))
              using (var rd= new StreamReader(xls_file.OpenReadStream())) {
                schema.CalcModelData= Convert.FromBase64String(rd.ReadToEnd());
            }
            else using (var bin = new BinaryReader(xls_file.OpenReadStream())) {
              schema.CalcModelData= bin.ReadBytes((int)xls_file.Length);
            }
          }

          /*Check validation syntax and calc. model:
           */
          docProcRepo.CreateDocumentProcessor<AbstractDocument>(schema).Dispose();

          DocumentSchema oldSchema;
          if (repo.TryGetByTypeId(schema.TypeId, out oldSchema))
            repo.Delete(oldSchema);
          repo.Insert(schema);
          repo.Store.CommitChanges();
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