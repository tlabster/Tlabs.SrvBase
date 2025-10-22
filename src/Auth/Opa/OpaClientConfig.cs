namespace Tlabs.Server.Auth.Opa {
  /// <summary>
  /// Configuration of the OPA client
  /// </summary>
  public class OpaClientConfig {
    /// <summary> URI of the OPA Server </summary>
    public string OpaUri { get; set; } = "http://localhost:8181";

    /// <summary> Path where the default policy is evaluated </summary>
    public string DefaultPolicyPath { get; set; } = "/v1/data/authz";
  }
}