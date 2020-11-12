using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

using Tlabs.Data;

namespace Tlabs.Server.Controller {

  ///<summary>API action base controller.</summary>
  public class ApiCtrl : Microsoft.AspNetCore.Mvc.Controller {
    static readonly ILogger log= Tlabs.App.Logger<ApiCtrl>();

    ///<summary>Resolved status code.</summary>
    protected int? ResolvedStatusCode;

    ///<summary>Resolve API error code from exception.</summary>
    protected virtual string resolveError(string errCode, Exception e, string msg0 = null) {
      e.Source= errCode ?? e.Source;
      return resolveError(e, msg0);
    }

    ///<summary>Resolve API error from exception.</summary>
    protected virtual string resolveError(Exception e, string msg0= null) {
      var inner= e.InnerException;
      var code= StatusCodes.Status500InternalServerError;
      var msg= msg0;

      switch (e) {
        case DataEntityNotFoundException nfe:
          code= StatusCodes.Status404NotFound;
          msg= msg ?? nfe.Message;
        break;

        case ArgumentNullException an:
          code= StatusCodes.Status400BadRequest;
          msg= msg ?? an.SetMissingTemplateData("Missing required parameter '{paramName}'", an.ParamName ?? an.Message).ResolvedMsgTemplate();
          break;

        case ArgumentOutOfRangeException re:
          code= StatusCodes.Status400BadRequest;
          msg= msg ?? re.SetMissingTemplateData("Value ({actualValue}) for parameter '{paramName}'", re.ActualValue ?? "-?-", re.ParamName ?? re.Message).ResolvedMsgTemplate();
        break;

        case ArgumentException ae:
          code= StatusCodes.Status400BadRequest;
          msg= msg ?? ae.SetMissingTemplateData("Invalid value for parameter '{paramName}'", ae.ParamName ?? ae.Message).ResolvedMsgTemplate();
        break;

        case KeyNotFoundException kn:
          code= StatusCodes.Status404NotFound;
          msg= msg ?? kn.Message;
        break;

        case InvalidOperationException io:
          code= StatusCodes.Status404NotFound;
          msg= msg ?? io.Message;
        break;

        case InvalidCastException ic:
          code= StatusCodes.Status400BadRequest;
          msg= msg ?? ic.SetMissingTemplateData("Invalid parameter type ({type})", ic.Message).ResolvedMsgTemplate();
        break;

        case FormatException fe:
          code= StatusCodes.Status400BadRequest;
          msg= msg ?? fe.SetMissingTemplateData("Invalid parameter format ({format})", fe.Message).ResolvedMsgTemplate();
        break;

        default:
          if (null != e.InnerException)
            return resolveError(e.InnerException, msg ?? e.Message);
          msg= e.SetTemplateData("Unsupported internal state - please check with log.").ResolvedMsgTemplate();
          log.LogError(e, "Error processing request ({msg}).", e.Message);
        break;
      }

      log.LogDebug(0, e, msg);
      if (null != HttpContext)
        HttpContext.Response.StatusCode= code;
      ResolvedStatusCode= code;
      return msg;
    }
  }
}