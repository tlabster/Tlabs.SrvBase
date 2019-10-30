﻿using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

using Tlabs.Data;

namespace Tlabs.Server.Controller {

  ///<summary>API action base controller.</summary>
  public class ApiCtrl : Microsoft.AspNetCore.Mvc.Controller {
    static readonly ILogger log= Tlabs.App.Logger<ApiCtrl>();

    ///<summary>Resolve API error code from exception.</summary>
    protected virtual string resolveError(Exception e) {
      var inner= e.InnerException;
      var code= StatusCodes.Status500InternalServerError;
      var msg= "Unsupported internal state - please ckeck with log.";

      switch (e) {
        case DataEntityNotFoundException nfe:
          code= StatusCodes.Status404NotFound;
          msg= nfe.Message;
        break;

        case ArgumentNullException an:
          code= StatusCodes.Status400BadRequest;
          msg= $"Missing required parameter '{an.ParamName}'";
          break;

        case ArgumentException ae:
          code= StatusCodes.Status400BadRequest;
          msg= ae.Message;
        break;

        case KeyNotFoundException kn:
          code= StatusCodes.Status404NotFound;
          msg= kn.Message;
        break;

        case InvalidOperationException io:
          code= StatusCodes.Status404NotFound;
          msg= io.Message;
        break;

        case InvalidCastException ic:
          code= StatusCodes.Status400BadRequest;
          msg= $"Invalid parameter type ({ic.Message})";
        break;

        case FormatException fe:
          code= StatusCodes.Status400BadRequest;
          msg= $"Invalid parameter format ({fe.Message})";
        break;

        default:
          if (null != e.InnerException)
            return resolveError(e.InnerException);
          log.LogError(0, e, msg);
          break;
      }

      log.LogDebug(0, e, msg);
      HttpContext.Response.StatusCode= code;
      return msg;
    }
  }
}