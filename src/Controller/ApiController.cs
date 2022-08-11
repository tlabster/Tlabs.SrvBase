using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

using Tlabs.Data;

namespace Tlabs.Server.Controller {

  ///<summary>API action base controller.</summary>
  public class ApiCtrl : Microsoft.AspNetCore.Mvc.ControllerBase {
    static readonly ILogger log= Tlabs.App.Logger<ApiCtrl>();

    ///<summary>Resolved status code.</summary>
    protected int? ResolvedStatusCode;

    ///<summary>Unique descriptor of the currently executing action.</summary>
    public string ActionRoute => $"{ControllerContext.ActionDescriptor?.ControllerName}/{ControllerContext.ActionDescriptor?.ActionName}";

    ///<summary>Resolve API error from exception.</summary>
    protected virtual string resolveError(Exception e, string msg0= null) {
      e.Source= ActionRoute;
      // var inner= e.InnerException;
      var code= StatusCodes.Status500InternalServerError;
      var msg= msg0;

      switch (e) {
        case DataEntityNotFoundException nfe:
          code= StatusCodes.Status404NotFound;
          msg??= nfe.Message;
        break;

        case ArgumentNullException an:
          code= StatusCodes.Status400BadRequest;
          msg??= an.SetMissingTemplateData("Missing required parameter '{paramName}'", an.ParamName ?? an.Message).ResolvedMsgTemplate();
          break;

        case ArgumentOutOfRangeException re:
          code= StatusCodes.Status400BadRequest;
          msg??= re.SetMissingTemplateData("Value ({actualValue}) for parameter '{paramName}'", re.ActualValue ?? "-?-", re.ParamName ?? re.Message).ResolvedMsgTemplate();
        break;

        case ArgumentException ae:
          code= StatusCodes.Status400BadRequest;
          msg??= ae.SetMissingTemplateData("Invalid value for parameter '{paramName}'", ae.ParamName ?? ae.Message).ResolvedMsgTemplate();
        break;

        case KeyNotFoundException kn:
          code= StatusCodes.Status404NotFound;
          msg??= kn.Message;
        break;

        case InvalidOperationException io:
          code= StatusCodes.Status404NotFound;
          msg??= io.Message;
        break;

        case InvalidCastException ic:
          code= StatusCodes.Status400BadRequest;
          msg??= ic.SetMissingTemplateData("Invalid parameter type ({type})", ic.Message).ResolvedMsgTemplate();
        break;

        case FormatException fe:
          code= StatusCodes.Status400BadRequest;
          msg??= fe.SetMissingTemplateData("Invalid parameter format ({format})", fe.Message).ResolvedMsgTemplate();
        break;

        default:
          if (null != e.InnerException)
            return resolveError(e.InnerException, msg ?? e.Message);
          msg= e.SetMissingTemplateData("Unsupported internal state - please check with log.").ResolvedMsgTemplate();
          log.LogError(e, "Error processing request ({msg}).", e.Message);
        break;
      }
#pragma warning disable CA2254  //log generic message
      log.LogDebug(0, e, msg);
#pragma warning restore CA2254
      if (null != HttpContext)
        HttpContext.Response.StatusCode= code;
      ResolvedStatusCode= code;
      return msg;
    }
  }
}