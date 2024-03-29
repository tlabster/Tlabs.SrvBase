﻿#pragma warning disable CS1591

using System;

namespace Tlabs.Server.Model {
  public class AuditRecord {
    public string? Method { get; set; }
    public string? ActionName { get; set; }
    public string? URL { get; set; }
    public string? BodyData { get; set; }
    public string? IPAddress { get; set; }
    public DateTime AccessTime { get; set; }
    public string? Editor { get; set; }
    public string? StatusCode { get; set; }
    public string? Error { get; set; }
    public bool Success { get; set; }

    public static AuditRecord FromEntity(Data.Entity.AuditRecord ent) => new AuditRecord {
      Method= ent.Method,
      ActionName= ent.ActionName,
      URL= ent.URL,
      BodyData= ent.BodyData,
      IPAddress= ent.IPAddress,
      AccessTime= ent.Modified,
      Editor= ent.Editor,
      StatusCode= ent.StatusCode,
      Error= ent.Error,
      Success= ent.Success
    };
  }

}