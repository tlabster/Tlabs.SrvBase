using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Tlabs.Config;
using Tlabs.Data.Serialize.Json;
using Tlabs.Server.Auth.Keycloak;

namespace Tlabs.Server.Auth.Opa {
  /// <summary>
  /// Result from evaluating an OPA policy
  /// </summary>
  /// <typeparam name="TResult">Type of the result object</typeparam>
  public class OpaResult<TResult> {
    /// <summary> Result of the OPA evaluation </summary>
    public TResult? Result { get; set; }
  }

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
    /// Evaluate an OPA policy with a generic <typeparamref name="TResponse"/> with a <paramref name="path"/> and a given <paramref name="input"/>
    /// </summary>
    /// <returns>The <typeparamref name="TResponse"/> resulting of the policies evaluation</returns>
    Task<TResponse?> EvaluateAsync<TResponse>(string path, OpaInput input, CancellationToken cancellationToken = default);
    /// <summary>
    /// Evaluate an <see cref="OpaDecision{TConstraints}"/>
    /// </summary>
    /// <param name="input">Input object for the OPA Policy</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The <see cref="OpaDecision{TConstraints}"/> resulting of the policies evaluation</returns>
    Task<OpaDecision<TConstraints?>> EvaluateDecisionAsync<TConstraints>(OpaInput input, CancellationToken cancellationToken = default);
    /// <summary>
    /// Evaluate a decision on a specific path
    /// </summary>
    /// <typeparam name="TConstraints">Type of the constraints part of the response</typeparam>
    /// <param name="input">Input object for the OPA Policy</param>
    /// <param name="path">Path of the policy to be evaluated</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The <see cref="OpaDecision{TConstraints}"/> resulting of the policies evaluation</returns>
    Task<OpaDecision<TConstraints?>> EvaluateDecisionAsync<TConstraints>(string path, OpaInput input, CancellationToken cancellationToken = default);
  }

  /// <summary>Implementation of an OPA Client</summary>
  public class OpaClient : IOpaClient {
    private readonly IHttpClientFactory httpClientFactory;
    private readonly string defaultPolicyPath;
    private const string OpaHttpClientName = "Opa";
    static readonly ILogger log = Tlabs.App.Logger<OpaClient>();

    /// <summary>
    /// Creator from <paramref name="httpClientFactory"/> and <paramref name="configOptions"/>
    /// </summary>
    public OpaClient(IHttpClientFactory httpClientFactory, IOptions<OpaClientConfig> configOptions) {
      this.httpClientFactory = httpClientFactory;
      var config = configOptions.Value;
      defaultPolicyPath = config.DefaultPolicyPath;
    }

    /// <inheritdoc/>
    public async Task<TResponse?> EvaluateAsync<TResponse>(string path, OpaInput input, CancellationToken cancellationToken = default) {
      var response = await QueryPolicy(path, input, cancellationToken);

      if (response == null) {
        log.LogCritical("Error querying policy on {path}: no response", path);
        return default;
      }

      var seri = JsonFormat.CreateSerializer<OpaResult<TResponse>>();
      log.LogDebug("Path: {path}", path);
      log.LogDebug("Response from OPA: {response}", response);
      OpaResult<TResponse>? opaResult = default;
      try {
        opaResult = seri.LoadObj(response);
      }
      catch (Exception ex) {
        log.LogCritical(ex, "Error querying policy on {path}: empty result", path);
        return default;
      }

      if (opaResult == null || opaResult.Result == null) {
        log.LogCritical("Error querying policy on {path}: invalid result {doc}", path, response);
        return default;
      }

      return opaResult.Result;
    }

    /// <inheritdoc/>
    public async Task<OpaDecision<TConstraints?>> EvaluateDecisionAsync<TConstraints>(OpaInput input, CancellationToken cancellationToken = default) {
      return await EvaluateDecisionAsync<TConstraints>(defaultPolicyPath, input, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<OpaDecision<TConstraints?>> EvaluateDecisionAsync<TConstraints>(string path, OpaInput input, CancellationToken cancellationToken = default) {
      var response = await QueryPolicy(path, input, cancellationToken);

      if (response == null) {
        log.LogCritical("Error querying policy on {path}: no response", defaultPolicyPath);
        return new OpaDecision<TConstraints?> { Allow = false };
      }

      var seri = JsonFormat.CreateSerializer<OpaResult<OpaDecision<TConstraints?>>>();
      OpaResult<OpaDecision<TConstraints?>>? opaResult = null;
      try {
        opaResult = seri.LoadObj(response);
      }
      catch (Exception ex) {
        log.LogCritical(ex, "Error querying policy on {path}: empty result", defaultPolicyPath);
        return new OpaDecision<TConstraints?> { Allow = false };
      }

      if (opaResult == null || opaResult.Result == null) {
        log.LogCritical("Error querying policy on {path}: invalid result {doc}", defaultPolicyPath, response);
        return new OpaDecision<TConstraints?> { Allow = false };
      }

      return opaResult.Result;
    }

    private async Task<string?> QueryPolicy(string path, OpaInput input, CancellationToken cancellationToken) {
      var httpClient = httpClientFactory.CreateClient(OpaHttpClientName);
      var request = new Dictionary<string, OpaInput> { { "input", input } };

      var requestSerializer = JsonFormat.CreateSerializer<Dictionary<string, OpaInput>>();
      var msgBytes = requestSerializer.WriteObj(request);
      var content = new ByteArrayContent(msgBytes);
      content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

      var response = await httpClient.PostAsync(path, content, cancellationToken);
      var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
      if (!response.IsSuccessStatusCode || responseContent == null) {
        log.LogCritical("Error querying policy on {path} with status: `{status}` and response: {content}",
                        path, response.StatusCode, content);
        return null;
      }

      return responseContent;
    }

    /// <summary>Configures the OPA Client to be used individually</summary>
    public class Configurator : IConfigurator<IServiceCollection> {
      /// <inheritdoc/>
      public void AddTo(IServiceCollection services, IConfiguration cfg) {
        var config = cfg.GetSection("config");
        services.Configure<OpaClientConfig>(config);
        var uri = config["OpaUri"]?.TrimEnd('/') ?? "http://localhost:8181";
        services.AddHttpClient(OpaHttpClientName, httpClient => {
          httpClient.BaseAddress = new Uri(uri);
        });
        services.AddScoped<IClaimsTransformation, KeycloakRolesClaimsTransformation>();
        services.AddSingleton<IOpaClient, OpaClient>();
        log.LogInformation("Service {s} added.", nameof(OpaClient));
      }
    }
  }
}