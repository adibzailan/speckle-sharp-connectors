using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Speckle.Connectors.Common.Caching;
using Speckle.Sdk.Api;
using Speckle.Sdk.Credentials;

namespace Speckle.Connectors.Common.GraphQL;

public class GraphQLDebugService : IGraphQLDebugService
{
  private readonly IClientFactory _clientFactory;
  private readonly IAccountManager _accountManager;
  private readonly ILogger<GraphQLDebugService> _logger;
  private readonly HttpClient _httpClient;

  public GraphQLDebugService(
    IClientFactory clientFactory,
    IAccountManager accountManager,
    ILogger<GraphQLDebugService> logger
  )
  {
    _clientFactory = clientFactory;
    _accountManager = accountManager;
    _logger = logger;
    _httpClient = new HttpClient();
  }

  public async Task<JsonElement> GetCommitDetails(string projectId, string modelId, int limit = 10)
  {
    var query = @"
      query GetCommits($projectId: String!, $modelId: String!, $limit: Int!) {
        project(id: $projectId) {
          model(id: $modelId) {
            versions(limit: $limit) {
              items {
                id
                message
                createdAt
                referencedObject
              }
            }
          }
        }
      }";

    var variables = new { projectId, modelId, limit };
    return await ExecuteQuery(query, variables);
  }

  public async Task<JsonElement> GetObjectData(string streamId, string objectId, int depth = 3, int limit = 100)
  {
    var query = @"
      query GetObjectData($streamId: String!, $objectId: String!, $depth: Int!, $limit: Int!) {
        stream(id: $streamId) {
          object(id: $objectId) {
            totalChildrenCount
            data
            children(limit: $limit, depth: $depth) {
              objects {
                id
                speckleType
                data
              }
            }
          }
        }
      }";

    var variables = new { streamId, objectId, depth, limit };
    return await ExecuteQuery(query, variables);
  }

  public async Task<BrepCheckResult> CheckBrepPresence(string streamId, string objectId)
  {
    var result = new BrepCheckResult();
    
    try
    {
      var objectData = await GetObjectData(streamId, objectId, depth: 5, limit: 1000);
      var jsonString = objectData.ToString();
      
      // Search for BREP objects
      if (jsonString.Contains("\"speckle_type\":\"Objects.Geometry.Brep\""))
      {
        result.HasBrep = true;
        result.BrepCount = CountOccurrences(jsonString, "\"speckle_type\":\"Objects.Geometry.Brep\"");
      }
      
      // Count mesh objects for comparison
      result.MeshCount = CountOccurrences(jsonString, "\"speckle_type\":\"Objects.Geometry.Mesh\"");
      
      // Parse JSON to find specific BREP object IDs
      ParseBrepDetails(objectData, result);
      
      _logger.LogInformation(
        "BREP Check - Found: {HasBrep}, BREP Count: {BrepCount}, Mesh Count: {MeshCount}",
        result.HasBrep,
        result.BrepCount,
        result.MeshCount
      );
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error checking BREP presence");
    }
    
    return result;
  }

  public async Task<GeometryTypesResult> GetGeometryTypes(string streamId, string objectId, int depth = 10, int limit = 1000)
  {
    var result = new GeometryTypesResult();
    
    try
    {
      var objectData = await GetObjectData(streamId, objectId, depth, limit);
      ParseGeometryTypes(objectData, result);
      
      _logger.LogInformation(
        "Found {TotalObjects} objects with {UniqueTypeCount} unique geometry types",
        result.TotalObjects,
        result.UniqueTypes.Count
      );
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting geometry types");
    }
    
    return result;
  }

  public async Task<JsonElement> ExecuteQuery(string query, object? variables = null)
  {
    var account = _accountManager.GetDefaultAccount();
    if (account == null)
    {
      throw new InvalidOperationException("No default Speckle account found");
    }

    var endpoint = new Uri(new Uri(account.serverInfo.url), "graphql").ToString();
    
    var request = new
    {
      query,
      variables
    };

    var json = JsonSerializer.Serialize(request);
    var content = new StringContent(json, Encoding.UTF8, "application/json");

    _httpClient.DefaultRequestHeaders.Clear();
    _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {account.token}");
    _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

    var response = await _httpClient.PostAsync(endpoint, content);
    response.EnsureSuccessStatusCode();

    var responseJson = await response.Content.ReadAsStringAsync();
    var document = JsonDocument.Parse(responseJson);
    
    if (document.RootElement.TryGetProperty("errors", out var errors))
    {
      var errorMessage = errors.ToString();
      _logger.LogError("GraphQL errors: {Errors}", errorMessage);
      throw new InvalidOperationException($"GraphQL query failed: {errorMessage}");
    }

    return document.RootElement.GetProperty("data");
  }

  private static int CountOccurrences(string text, string pattern)
  {
    int count = 0;
    int index = 0;
    while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) != -1)
    {
      count++;
      index += pattern.Length;
    }
    return count;
  }

  private void ParseBrepDetails(JsonElement element, BrepCheckResult result)
  {
    try
    {
      if (element.ValueKind == JsonValueKind.Object)
      {
        // Check if this object is a BREP
        if (element.TryGetProperty("speckleType", out var typeElement) &&
            typeElement.GetString() == "Objects.Geometry.Brep" &&
            element.TryGetProperty("id", out var idElement))
        {
          result.BrepObjectIds.Add(idElement.GetString() ?? "");
        }

        // Check if this is an element with BREP in displayValue
        if (element.TryGetProperty("displayValue", out var displayValue) && 
            displayValue.ValueKind == JsonValueKind.Array)
        {
          foreach (var item in displayValue.EnumerateArray())
          {
            if (item.TryGetProperty("speckle_type", out var itemType) &&
                itemType.GetString() == "Objects.Geometry.Brep" &&
                element.TryGetProperty("name", out var nameElement))
            {
              result.ElementsWithBrep.Add(nameElement.GetString() ?? "");
            }
          }
        }

        // Recursively check all properties
        foreach (var property in element.EnumerateObject())
        {
          ParseBrepDetails(property.Value, result);
        }
      }
      else if (element.ValueKind == JsonValueKind.Array)
      {
        foreach (var item in element.EnumerateArray())
        {
          ParseBrepDetails(item, result);
        }
      }
    }
    catch (Exception ex)
    {
      _logger.LogDebug(ex, "Error parsing BREP details");
    }
  }

  private void ParseGeometryTypes(JsonElement element, GeometryTypesResult result)
  {
    try
    {
      if (element.ValueKind == JsonValueKind.Object)
      {
        if (element.TryGetProperty("speckleType", out var typeElement))
        {
          var type = typeElement.GetString() ?? "";
          if (type.StartsWith("Objects.Geometry."))
          {
            result.TotalObjects++;
            
            if (!result.TypeCounts.ContainsKey(type))
            {
              result.TypeCounts[type] = 0;
              result.UniqueTypes.Add(type);
            }
            result.TypeCounts[type]++;
          }
        }

        foreach (var property in element.EnumerateObject())
        {
          ParseGeometryTypes(property.Value, result);
        }
      }
      else if (element.ValueKind == JsonValueKind.Array)
      {
        foreach (var item in element.EnumerateArray())
        {
          ParseGeometryTypes(item, result);
        }
      }
    }
    catch (Exception ex)
    {
      _logger.LogDebug(ex, "Error parsing geometry types");
    }
  }
}