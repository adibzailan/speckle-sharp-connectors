# GraphQL Debugging Tool Implementation Plan

**Date**: 2025-07-05  
**Status**: Proposed  

## Overview

This document outlines a plan to implement a GraphQL debugging tool for Speckle that allows developers to query and inspect Speckle data directly from their IDE without requiring the web frontend. This tool will significantly improve the debugging workflow for geometry conversion issues, particularly for BREP data transfer validation.

## Motivation

Currently, developers must use the Speckle web interface to inspect data sent to Speckle Cloud, which creates context-switching overhead during development. A dedicated debugging tool integrated into the development environment would:

1. Streamline the debugging process
2. Enable programmatic verification of data structures
3. Allow automated testing of conversion results
4. Provide a consistent interface for querying Speckle data

## Implementation Plan

### 1. Core GraphQL Client

Create a reusable `SpeckleGraphQLClient` class that handles:
- Authentication with Speckle API
- Executing GraphQL queries
- Parsing and returning results
- Helper methods for common queries

```csharp
public class SpeckleGraphQLClient
{
    private readonly HttpClient _httpClient;
    private readonly string _endpoint = "https://app.speckle.systems/graphql";
    private readonly string _token;

    public SpeckleGraphQLClient(string token)
    {
        _token = token;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", _token);
    }

    public async Task<JsonElement> ExecuteQuery(string query, object variables = null)
    {
        // Implementation details
    }

    // Helper methods for common queries
    public async Task<JsonElement> GetCommitDetails(string projectId, string modelId, int limit = 10)
    {
        // Implementation details
    }

    public async Task<JsonElement> GetObjectData(string projectId, string objectId)
    {
        // Implementation details
    }
}
```

### 2. Standalone Debugging Tool

Create a command-line tool that can be used independently:

```
SpeckleGraphQLDebugger.exe --project-id d700651b58 --model-id 9de6eef31d --check-brep
```

This tool will:
- Accept command-line arguments for project/model/object IDs
- Support common debugging operations (list commits, check for BREP data, etc.)
- Output results in a readable format (JSON, table, etc.)
- Support saving results to file for comparison

### 3. IDE Integration

#### Visual Studio Extension

Create a Visual Studio extension that:
- Adds a "Speckle GraphQL Explorer" tool window
- Provides a UI for executing queries
- Displays results in a tree view
- Allows saving queries for reuse

#### VS Code Extension

Create a VS Code extension that:
- Adds a Speckle sidebar
- Provides a GraphQL query editor
- Displays results with syntax highlighting
- Integrates with the debugging workflow

### 4. Connector Integration

Add debugging capabilities directly to connectors:

```csharp
public class DebugCommands
{
    private readonly SpeckleGraphQLClient _client;
    
    public DebugCommands(SpeckleGraphQLClient client)
    {
        _client = client;
    }
    
    public async Task VerifyLastSentCommit(string modelCardId)
    {
        // Get the last commit for this model card
        // Check for BREP data
        // Display results
    }
}
```

## Key Features

### 1. BREP Verification

Specific queries to verify BREP data transfer:

```graphql
query CheckBrepData($streamId: String!, $objectId: String!) {
  stream(id: $streamId) {
    object(id: $objectId) {
      data
      children(limit: 100, depth: 3) {
        objects {
          id
          speckleType
          data
        }
      }
    }
  }
}
```

Helper method to check for BREP data:

```csharp
public async Task<bool> CheckForBrepData(string projectId, string objectId)
{
    var result = await GetObjectData(projectId, objectId);
    var resultString = result.ToString();
    return resultString.Contains("\"speckleType\":\"Objects.Geometry.Brep\"");
}
```

### 2. Geometry Statistics

Generate statistics about sent geometry:

- Count of each geometry type (mesh, BREP, curve, etc.)
- Percentage of BREP vs mesh conversion
- Conversion success/failure rates
- Performance metrics

### 3. Diff Functionality

Compare two commits to identify changes:

- Added/removed objects
- Changed geometry types
- Modified properties
- Visual diff of geometry (via exported thumbnails)

## Technical Requirements

- .NET 8.0 or .NET Framework 4.8 (for compatibility with connectors)
- System.Net.Http for API communication
- System.Text.Json for JSON parsing
- Authentication via Speckle token (from Speckle Manager or environment variable)
- GraphQL query validation

## Project Structure

```
Tools/
└── GraphQLDebugger/
    ├── SpeckleGraphQLClient.cs       # Core client
    ├── Program.cs                    # CLI entry point
    ├── Commands/                     # CLI commands
    │   ├── CommitCommand.cs
    │   ├── ObjectCommand.cs
    │   └── BrepCheckCommand.cs
    ├── Models/                       # Response models
    │   ├── CommitInfo.cs
    │   ├── ObjectInfo.cs
    │   └── BrepData.cs
    └── Extensions/                   # IDE extensions
        ├── VisualStudio/
        └── VSCode/
```

## Implementation Timeline

1. **Phase 1: Core Client** (1 week)
   - Implement SpeckleGraphQLClient
   - Create basic CLI tool
   - Add common queries

2. **Phase 2: Enhanced Features** (2 weeks)
   - Add BREP verification
   - Implement geometry statistics
   - Create diff functionality

3. **Phase 3: IDE Integration** (3 weeks)
   - Develop Visual Studio extension
   - Create VS Code extension
   - Add connector integration

## Testing Strategy

1. **Unit Tests**
   - Test client with mock HTTP responses
   - Verify query construction
   - Test helper methods

2. **Integration Tests**
   - Test against Speckle staging server
   - Verify with known test models
   - Test with various geometry types

3. **User Testing**
   - Developer workflow testing
   - Performance with large models
   - Usability feedback

## Conclusion

The GraphQL debugging tool will significantly improve the development workflow for Speckle connectors, particularly for complex geometry conversion like BREP. By providing direct access to Speckle data from within the IDE, developers can more efficiently debug and verify their implementations without context switching to the web interface.

This tool aligns with the recent BREP implementation work and will help ensure the quality and correctness of geometry conversion in future development.