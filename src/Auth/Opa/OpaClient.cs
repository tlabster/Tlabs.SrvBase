using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Reflection.Emit;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using Tlabs.Data.Serialize;

namespace Tlabs.Server.Auth.Opa {
  /// <summary>
  /// Class describing a decision from the OPA Middleware
  /// </summary>
  public class OpaDecision<TConstraints> {
    /// <summary>Action execution is allowed</summary>
    public bool Allow { get; set; }
    /// <summary>List of constraints</summary>
    public TConstraints? Constraints { get; set; }
  }

  /// <summary>Subject executing an action</summary>
  public class Subject {
    /// <summary>Username of the subject</summary>
    public string? User { get; set; }
    /// <summary>List of roles of the given subject</summary>
    public string[] Roles { get; set; } = Array.Empty<string>();
  }

  /// <summary>Input to be evaluated against an OPA Policy</summary>
  public class OpaInput {
    /// <summary>Name of the resource being evaluated</summary>
    public string? Resource { get; set; }
    /// <summary>Name of the resource being evaluated</summary>
    public string[] Path { get; set; } = Array.Empty<string>();
    /// <summary>Method [GET/POST/PUT...]</summary>
    public string? Method { get; set; }
    /// <summary>
    /// Subject executing the action
    /// </summary>
    public Subject Subject { get; set; } = new Subject();
    /// <summary>
    /// Extra information to be injected for additional needs
    /// </summary>
    public Dictionary<string, object>? AdditionalFields { get; set; }
  }

  /// <summary>Interface of a client for the OPA Service</summary>
  public interface IOpaClient {
    /// <summary>
    /// Evaluate an <see cref="OpaDecision{TConstraints}"/>
    /// </summary>
    /// <param name="input">Input object for the opa Policy</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The <see cref="OpaDecision{TConstraints}"/> resulting of the policies evaluation</returns>
    Task<OpaDecision<TConstraints?>> EvaluateAsync<TConstraints>(OpaInput input, CancellationToken cancellationToken = default);
  }

  /// <summary>Implementation of an OPA Client</summary>
  public class OpaClient : IOpaClient {
    private readonly IHttpClientFactory httpClientFactory;
    private readonly string policyPath;
    private readonly IDynamicSerializer serializer;
    static readonly ILogger log = Tlabs.App.Logger<OpaClient>();

    /// <summary>
    /// Creator from <paramref name="httpClientFactory"/>, <paramref name="serializer"/> and <paramref name="config"/>
    /// </summary>
    public OpaClient(IHttpClientFactory httpClientFactory, IDynamicSerializer serializer, OpaClientConfig config) {
      this.serializer = serializer;
      this.httpClientFactory = httpClientFactory;
      policyPath = $"{config.OpaUri.TrimEnd('/')}{config.PolicyPath}";
    }

    /// <inheritdoc/>
    public async Task<OpaDecision<TConstraints?>> EvaluateAsync<TConstraints>(OpaInput input, CancellationToken cancellationToken = default) {
      var httpClient = httpClientFactory.CreateClient();
      var request = new Dictionary<string, OpaInput> { { "input", input } };

      var msgBytes = serializer.WriteObj(request);
      var content = new ByteArrayContent(msgBytes);
      content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

      var response = await httpClient.PostAsync(policyPath, content, cancellationToken);
      var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

      if (!response.IsSuccessStatusCode) {
        log.LogCritical("Error querying policy on {path} with status: `{status}` and response: {content}",
                        policyPath, response.StatusCode, content);
        return new OpaDecision<TConstraints?> { Allow = false };
      }

      using var doc = JsonDocument.Parse(responseContent);
      if (!doc.RootElement.TryGetProperty("result", out var result)) {
        log.LogCritical("Error querying policy on {path}: invalid result {doc}", policyPath, doc);
        return new OpaDecision<TConstraints?> { Allow = false };
      }

      var decision = new OpaDecision<TConstraints?>
      {
        Allow = result.GetProperty("allow").GetBoolean(),
        Constraints = result.TryGetProperty("constraints", out var constraints)
                ? JsonSerializer.Deserialize<TConstraints>(constraints.GetRawText())
                : default
      };

      return decision;
    }
  }
}