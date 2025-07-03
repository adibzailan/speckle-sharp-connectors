# Revit Connector UI Improvements Plan

**Date**: 2025-07-05 (Updated)  
**Status**: Proposed  

## Overview

This document outlines the architecture of the Speckle for Revit connector UI and proposes improvements to enhance user experience, error reporting, and visual design. The current implementation uses web technologies embedded within Revit, providing opportunities for modern UI development while maintaining native integration.

## Current Architecture

### Web-Based UI Approach

The Speckle for Revit connector uses a web-based UI approach with two different technologies depending on the Revit version:

1. **WebView2** (Microsoft's modern web view control) for Revit 2026+
2. **CefSharp** (Chromium Embedded Framework) for older Revit versions (2022-2025)

This architecture consists of:

1. **Native Container**: C# code that creates a dockable panel in Revit
2. **Web Control**: WebView2 or CefSharp browser component
3. **Web Frontend**: HTML/CSS/JavaScript application hosted on Netlify
4. **Bridge**: Communication layer between web UI and Revit API

### Key Components

#### WebView2 Implementation (Revit 2026+)

```xml
<!-- RevitControlWebView.xaml -->
<UserControl ...>
  <DockPanel>
    <wv2:WebView2 
      Name="Browser"
      Source="{x:Static dui:Url.Netlify}" />
  </DockPanel>
</UserControl>
```

#### CefSharp Implementation (Revit 2022-2025)

```xml
<!-- CefSharpPanel.xaml -->
<Page ...>
  <Grid>
    <cefSharp:ChromiumWebBrowser
      Name="Browser"
      Address="{x:Static local:Url.NetlifyString}" />
  </Grid>
</Page>
```

#### Frontend Source

The UI content is loaded from a Netlify-hosted web application:

```csharp
// Url.cs
public static class Url
{
  public static readonly Uri Netlify = new("https://boisterous-douhua-e3cefb.netlify.app/");
  public static readonly string NetlifyString = Netlify.ToString();
}
```

#### Communication Bridge

The connector uses a bridge pattern to communicate between the web UI and Revit:

```csharp
// BrowserBridge.cs
public class BrowserBridge
{
  public async Task Send(string eventName, object data)
  {
    // Send data from C# to JavaScript
  }
  
  public void RegisterCallback(string eventName, Func<object, Task<object>> callback)
  {
    // Register C# callback for JavaScript events
  }
}
```

## Frontend Repository

The UI frontend code is maintained in a separate repository: [speckle-connectors-dui](https://github.com/specklesystems/speckle-connectors-dui).

### Repository Structure

- **Technology Stack**:
  - React with TypeScript
  - Vite build system
  - Tailwind CSS for styling

- **Key Directories**:
  - `src/` - Main source code
  - `src/components/` - UI components (cards, buttons, dialogs)
  - `src/bindings/` - JavaScript side of the bridge to communicate with C#
  - `src/pages/` - Different UI views and screens

- **Build Process**:
  - The UI is built and deployed to Netlify
  - The built URL is referenced in the `Url.cs` file in the .NET connector code

### Development Workflow

To modify the UI:

1. Clone the repository: `git clone https://github.com/specklesystems/speckle-connectors-dui.git`
2. Install dependencies: `npm install`
3. Run locally: `npm run dev`
4. Point connector to local server by modifying `Url.cs`
5. Build for production: `npm run build`

## Improvement Opportunities

### 1. Enhanced Error Reporting

Current issue: Error messages like "Revit operation failed" lack context and actionable information.

Proposed improvements:
- Add detailed error messages with specific failure reasons
- Include troubleshooting suggestions
- Implement error categorization (user error, system error, network error)
- Add error codes for documentation reference

Implementation approach:
```csharp
try {
  // Existing operation code
}
catch (Exception ex)
{
  string detailedError = $"Operation failed: {ex.Message}";
  string troubleshooting = GetTroubleshootingTips(ex);
  string errorCode = GenerateErrorCode(ex);
  
  await _bridge.Send("operationError", new { 
    message = detailedError,
    tips = troubleshooting,
    code = errorCode,
    category = DetermineErrorCategory(ex)
  });
}
```

### 2. UI/UX Improvements

Current issues:
- Dark theme may not match Revit's UI style
- Limited visual feedback during operations
- Error states could be more visually distinct

Proposed improvements:
- Add light/dark theme toggle to match Revit's appearance
- Implement progress indicators for long-running operations
- Redesign error states with clear visual cues
- Add tooltips and contextual help
- Improve information hierarchy and readability

Implementation approach:
- Update frontend CSS/HTML
- Add theme detection based on Revit's theme
- Implement new UI components for progress and errors

### 3. Performance Enhancements

Current issues:
- UI may freeze during complex operations
- Large model handling can be slow

Proposed improvements:
- Implement background processing with progress updates
- Add cancelable operations
- Optimize data transfer between Revit and UI
- Implement virtualization for large model lists

Implementation approach:
```csharp
public async Task SendWithProgress(string modelCardId)
{
  // Create progress reporter
  var progress = new Progress<CardProgress>(p => 
    _bridge.Send("sendProgress", p));
  
  // Run operation with progress reporting
  await Task.Run(() => _sendOperation.Execute(modelCardId, progress));
}
```

### 4. Offline Capabilities

Current issue: Requires internet connection to load UI from Netlify

Proposed improvement:
- Package web assets with connector for offline use
- Implement fallback mechanism when Netlify is unreachable
- Cache previously loaded resources

Implementation approach:
```csharp
public static class Url
{
  public static readonly Uri Netlify = new("https://boisterous-douhua-e3cefb.netlify.app/");
  public static readonly Uri Local = new("file:///C:/ProgramData/Speckle/Connectors/Revit/ui/index.html");
  
  public static Uri GetPreferredSource()
  {
    return IsOnline() ? Netlify : Local;
  }
}
```

## Implementation Strategies With Frontend Repository Access

Now that we have access to the frontend repository, here are strategies for implementing UI improvements:

### 1. Local Development Workflow

1. Clone the frontend repository:
   ```bash
   git clone https://github.com/specklesystems/speckle-connectors-dui.git
   cd speckle-connectors-dui
   npm install
   ```

2. Run the development server:
   ```bash
   npm run dev
   ```

3. Modify the connector's `Url.cs` to point to your local server:
   ```csharp
   public static readonly Uri Netlify = new("http://localhost:3000/");
   ```

4. Make UI changes and test them directly in Revit

### 2. Build and Embed Approach

1. Make changes to the frontend code
2. Build the UI:
   ```bash
   npm run build
   ```
3. Copy the built files to a local directory:
   ```bash
   cp -r dist/ C:/ProgramData/Speckle/Connectors/Revit/ui/
   ```
4. Modify `Url.cs` to use the local files:
   ```csharp
   public static readonly Uri Netlify = new("file:///C:/ProgramData/Speckle/Connectors/Revit/ui/index.html");
   ```

### 3. Coordinated Frontend-Backend Changes

For features requiring both UI and backend changes:

1. Create new bridge methods in C#:
   ```csharp
   public async Task<object> GetRevitTheme(object args)
   {
     // Determine if Revit is using dark or light theme
     return new { isDarkTheme = IsRevitUsingDarkTheme() };
   }
   ```

2. Add corresponding JavaScript in the frontend:
   ```javascript
   // In src/bindings/bridge.ts
   export async function getRevitTheme() {
     return await callBackend('getRevitTheme');
   }
   
   // In src/components/ThemeProvider.tsx
   useEffect(() => {
     getRevitTheme().then(({ isDarkTheme }) => {
       setTheme(isDarkTheme ? 'dark' : 'light');
     });
   }, []);
   ```

## Implementation Plan

### Phase 1: Error Handling Improvements (2 weeks)
- Enhance error reporting in binding classes
- Implement structured error objects
- Update frontend to display detailed errors
- Add logging for troubleshooting

### Phase 2: UI/UX Enhancements (3 weeks)
- Design and implement light/dark theme
- Add progress indicators
- Improve visual feedback
- Enhance information hierarchy

### Phase 3: Performance Optimizations (2 weeks)
- Implement background processing
- Add cancelable operations
- Optimize data transfer
- Test with large models

### Phase 4: Offline Capabilities (1 week)
- Package web assets with connector
- Implement fallback mechanism
- Add caching for resources

## Technical Considerations

### Cross-Version Compatibility
- Ensure UI improvements work across all supported Revit versions (2022-2026)
- Test with both WebView2 and CefSharp implementations
- Maintain backward compatibility with older Revit versions

### Frontend Development
- Set up local development environment for UI changes
- Implement build process for frontend assets
- Consider using modern web frameworks (React, Vue) for maintainability

### Testing Strategy
- Unit tests for error handling logic
- Visual regression tests for UI changes
- Performance benchmarks for optimizations
- User testing with real-world scenarios

## Integration with GraphQL Debugging Tool

The recently implemented GraphQL debugging tool can be leveraged to improve the UI experience:

1. **Error Diagnosis**: Use the GraphQL tool to diagnose issues when UI operations fail
2. **Data Verification**: Verify data structures before displaying in UI
3. **Performance Monitoring**: Track operation times and data sizes

Example integration:
```csharp
public async Task VerifyAndSend(string modelCardId)
{
  // Send data to Speckle
  var result = await _sendOperation.Execute(modelCardId);
  
  // Verify BREP data was sent correctly
  var brepCheck = await _graphQLDebugService.CheckBrepPresence(result.StreamId, result.ObjectId);
  
  // Update UI with verification results
  await _bridge.Send("sendComplete", new {
    success = true,
    brepVerified = brepCheck.HasBrep,
    brepCount = brepCheck.BrepCount
  });
}
```

## Conclusion

The web-based UI approach used by the Speckle for Revit connector provides significant opportunities for improvement while maintaining native integration. By enhancing error reporting, improving the visual design, optimizing performance, and adding offline capabilities, we can create a more robust and user-friendly experience for Speckle users within Revit.

With access to the frontend repository (speckle-connectors-dui), we can implement comprehensive UI improvements using a coordinated approach between the frontend and backend components. This enables us to create a more cohesive and responsive user experience while maintaining the benefits of the web-based UI architecture.
