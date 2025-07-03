# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview
This is the Speckle Sharp Connectors repository - a next-generation .NET project for Speckle's desktop connectors. It provides integrations with various CAD/BIM applications including Revit, Rhino, AutoCAD, Civil3D, Tekla Structures, and more.

## Development Commands

### Building and Testing
```powershell
# Format code (must pass before commits)
.\build.ps1 format

# Build the project
.\build.ps1 build

# Run tests for affected projects
.\build.ps1 test

# Run all tests
.\build.ps1 test-only

# Create deployment packages
.\build.ps1 zip
```

### Cleaning
```powershell
# Clean package locks (use when switching solutions)
.\build.ps1 clean-locks

# Full clean (removes bin/obj directories)
.\build.ps1 deep-clean
```

### Working with Local SDK Changes
When modifying the Speckle SDK alongside connectors:
1. Use `Local.sln` instead of `Speckle.Connectors.sln`
2. Clean locks after switching: `.\build.ps1 clean-locks`

## Architecture Overview

### Core Components
- **Connectors**: Host application integrations in `Connectors/` (e.g., `Connectors/Revit/`)
- **Converters**: Data transformation logic in `Converters/` (e.g., `Converters/Revit/`)
- **DUI3**: Desktop UI framework in `DUI3/` using CefSharp WebView
- **SDK**: Core libraries in `SDK/`

### Sending Data to Speckle Cloud

The process of pushing geometry to Speckle Cloud involves two main components:

1. **Connectors**: Orchestrate the sending process by:
   - Providing UI integration with host applications
   - Managing send operations through `SendBinding` implementations
   - Handling user interactions and model cards
   - Initiating the conversion and sending pipeline

2. **Converters**: Transform native geometry to Speckle format by:
   - Converting application-specific objects to Speckle objects
   - Implementing type-specific converters (points, meshes, BREPs, etc.)
   - Handling unit conversions and coordinate systems
   - Supporting fallback conversion paths when needed

The typical data flow:
```
Host App Object → Converter → Speckle Object → Connector Send Operation → Speckle Cloud
```

Example: When sending a Revit wall, the `RevitSendBinding` initiates the process, while `WallToSpeckleConverter` transforms the wall into a Speckle object before transmission.

### Key Patterns

#### Dependency Injection
- Uses Autofac with `ISpeckleModule` for modular registration
- Registration methods follow pattern: `AddXxx()` (e.g., `AddRevit()`, `AddConverters()`)
- Service lifetimes: Singleton, Scoped, Transient

#### Conversion Pipeline
- **Top-level converters**: `IToSpeckleTopLevelConverter`, `IToHostTopLevelConverter`
- **Raw converters**: `ITypedConverter<TFrom, TTo>`
- **ConverterManager**: Resolves converters dynamically by type and rank

#### Threading
- `ThreadContext` abstracts main/worker thread execution
- Host-specific implementations (e.g., `RevitThreadContext`)
- UI operations on main thread, heavy operations on worker threads

#### UI Communication (DUI3)
- `BrowserBridge` manages .NET ↔ JavaScript communication
- Bindings implement specific operations (Send, Receive, etc.)
- Events flow from backend to frontend via `Send()` methods

## Code Standards

### C# Configuration
- C# 12 with nullable reference types enabled
- Implicit usings enabled
- Warnings as errors in Release builds
- File-scoped namespaces preferred

### Formatting
- CSharpier with 120 character line width
- 2 spaces indentation
- Run `.\build.ps1 format` before committing

### Project Structure
- Shared projects (`.shproj`) for code reuse across versions
- Version-specific projects for each supported year (e.g., Revit2022, Revit2023)
- Clear separation between connectors and converters

## Testing
- NUnit 4.x for unit tests
- Test projects follow `*.Tests` naming convention
- Run single test: `dotnet test "path/to/TestProject.csproj" -c Release --filter "FullyQualifiedName~TestName"`

## Common Development Tasks

### Adding a New Converter
1. Create converter class implementing appropriate interface (`ITypedConverter<,>`)
2. Add `[NameAndRankValue]` attribute with appropriate rank
3. Register in converter module's DI configuration

### Working with Host Applications
- Each connector has a context class (e.g., `RevitContext`, `RhinoContext`)
- Document state managed via `DocumentStore`
- Host-specific settings in `ConversionSettings` classes

### Debugging Connectors
1. Set connector project as startup project
2. Debugger will launch host application
3. Host application paths configured in `launchSettings.json`

## Debugging and Verification

### Using GraphQL to Verify Speckle Data
When debugging geometry conversion (especially BREP), use Speckle's GraphQL API:

1. **GraphQL Endpoint**: https://app.speckle.systems/graphql
2. **Extract IDs from URL**: `projects/{projectId}/models/{modelId}`
3. **Key Queries**:
   ```graphql
   # Get commit details
   query {
     project(id: "PROJECT_ID") {
       model(id: "MODEL_ID") {
         versions(limit: 10) {
           items { id message createdAt referencedObject }
         }
       }
     }
   }
   
   # Check object data
   query {
     stream(id: "PROJECT_ID") {
       object(id: "OBJECT_ID") {
         data
         children(limit: 100, depth: 3) {
           objects { id speckleType data }
         }
       }
     }
   }
   ```

4. **Verify BREP Transfer**: Look for `speckle_type: "Objects.Geometry.Brep"` in displayValue arrays
5. **See**: `_DOCUMENTATION/_SOLUTION_REVIEW/GraphQL_Guide_Checking_BREP_Data.md` for detailed guide

### Common Debugging Steps
1. Check dependency injection registration
2. Verify converter settings (e.g., `SendAsBREP`)
3. Enable debug logging for converters
4. Use GraphQL to inspect sent data
5. Check Revit journal files for suppressed errors

## Important Notes
- Package versions centrally managed in `Directory.Packages.props`
- Always use `IRootToSpeckleConverter` and `IRootToHostConverter` interfaces
- Caching implemented via `SendConversionCache` - leverage for performance
- Progress reporting via `IProgress<CardProgress>` throughout operations
- Use GraphQL for debugging data transfer issues
