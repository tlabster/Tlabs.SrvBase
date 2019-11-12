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
    protected virtual string resolveError(Exception e, string msg0= null) {
      var inner= e.InnerException;
      var code= StatusCodes.Status500InternalServerError;
      var msg= msg0 ?? e.Message;

      switch (e) {
        case DataEntityNotFoundException nfe:
          code= StatusCodes.Status404NotFound;
          msg= msg ?? nfe.Message;
        break;

        case ArgumentNullException an:
          code= StatusCodes.Status400BadRequest;
          msg= msg ?? $"Missing required parameter '{an.ParamName}'";
          break;

        case ArgumentException ae:
          code= StatusCodes.Status400BadRequest;
          msg= msg ?? ae.Message;
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
          msg= msg ?? $"Invalid parameter type ({ic.Message})";
        break;

        case FormatException fe:
          code= StatusCodes.Status400BadRequest;
          msg= msg ?? $"Invalid parameter format ({fe.Message})";
        break;

        default:
          if (null != e.InnerException)
            return resolveError(e.InnerException, msg);
          msg= "Unsupported internal state - please ckeck with log.";
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