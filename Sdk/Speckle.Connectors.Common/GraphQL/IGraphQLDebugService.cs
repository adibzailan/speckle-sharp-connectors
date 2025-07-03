using System.Text.Json;

namespace Speckle.Connectors.Common.GraphQL;

public interface IGraphQLDebugService
{
  /// <summary>
  /// Gets commit details for a specific model
  /// </summary>
  Task<JsonElement> GetCommitDetails(string projectId, string modelId, int limit = 10);

  /// <summary>
  /// Gets object data with specified depth
  /// </summary>
  Task<JsonElement> GetObjectData(string streamId, string objectId, int depth = 3, int limit = 100);

  /// <summary>
  /// Checks if BREP data is present in an object's display value
  /// </summary>
  Task<BrepCheckResult> CheckBrepPresence(string streamId, string objectId);

  /// <summary>
  /// Gets all geometry types present in an object hierarchy
  /// </summary>
  Task<GeometryTypesResult> GetGeometryTypes(string streamId, string objectId, int depth = 10, int limit = 1000);

  /// <summary>
  /// Executes a custom GraphQL query
  /// </summary>
  Task<JsonElement> ExecuteQuery(string query, object? variables = null);
}

public class BrepCheckResult
{
  public bool HasBrep { get; set; }
  public int BrepCount { get; set; }
  public int MeshCount { get; set; }
  public List<string> BrepObjectIds { get; set; } = new();
  public List<string> ElementsWithBrep { get; set; } = new();
}

public class GeometryTypesResult
{
  public Dictionary<string, int> TypeCounts { get; set; } = new();
  public int TotalObjects { get; set; }
  public List<string> UniqueTypes { get; set; } = new();
}