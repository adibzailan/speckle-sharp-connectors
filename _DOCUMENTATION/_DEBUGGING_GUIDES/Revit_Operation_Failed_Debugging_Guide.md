# Revit Operation Failed - Debugging Guide

## Overview
This guide helps debug the "Revit operation failed" error when pushing data to Speckle Cloud. The improvements made to error handling will provide more specific error messages and context.

## Quick Debugging Steps

### 1. Check the Enhanced Error Message
With the recent improvements, you should now see more specific error messages like:
- `Invalid Revit operation: [specific details]`
- `Element has been deleted: [element info]`
- `Failed to convert Wall element 'Basic Wall' (ID: 123456)`

### 2. Enable Debug Logging
Set the environment variable to enable detailed logging:
```bash
SPECKLE_LOG_LEVEL=Debug
```

### 3. Check Revit Journal File
Navigate to the Revit journal file location:
- Windows: `%LOCALAPPDATA%\Autodesk\Revit\[Version]\Journals\`
- Look for the most recent `.txt` file
- Search for exceptions near the timestamp of your error

### 4. Use GraphQL Debugger Tool
After a failed send attempt, use the debugging tool:
```bash
# Get project and object IDs from the Speckle URL
dotnet run --project Tools/Speckle.GraphQL.Debugger -- check-brep -p PROJECT_ID -o OBJECT_ID

# Check recent commits
dotnet run --project Tools/Speckle.GraphQL.Debugger -- commits -p PROJECT_ID -m MODEL_ID -l 10

# Analyze geometry types
dotnet run --project Tools/Speckle.GraphQL.Debugger -- types -p PROJECT_ID -o OBJECT_ID
```

## Common Error Patterns and Solutions

### 1. "Failed to convert all objects" Error
**New Enhanced Error Format (as of 2025-01-03):**
```
Failed to convert all X objects.
Errors: Y, Skipped: Z

Error Summary:
  - SpeckleException: 15 occurrences
  - InvalidOperationException: 3 occurrences

First 3 errors:
  - Wall (4d5e6f7g-8h9i): Failed to convert Wall element 'Basic Wall' (ID: 123456): [specific error]
  - Floor (1a2b3c4d-5e6f): Invalid geometry in element
```

**Common Causes:**
- Geometry conversion failures
- Invalid or corrupt elements
- Missing required parameters
- Unsupported element types

**Debugging Steps:**
1. Look at the error summary to identify patterns
2. Check the specific error messages for the first few failures
3. Try sending just one of the failed elements to isolate the issue
4. Check element validity in Revit (use Warnings dialog)
5. Enable debug logging to see full conversion details

### 2. BREP Conversion Failures
**Symptoms:**
- Error messages mentioning "BREP conversion failed"
- Geometry appears as mesh instead of smooth surfaces in Rhino

**Solutions:**
- Check logs for "BREP converter is null" - indicates DI issue
- Look for "BREP conversion completed with X successes and Y failures"
- Temporarily disable BREP: Set `SendAsBREP` to `false` in settings

### 2. Element Conversion Errors
**New Enhanced Error Format:**
```
Failed to convert Wall element 'Basic Wall: 200mm' (ID: 401234) from linked model: [specific error]
```

**Common Causes:**
- Invalid geometry in the element
- Missing parameters
- Linked model access issues
- Deleted or invalid elements

### 3. Transaction Errors
**Symptoms:**
- `Wrong transaction status` errors
- `Operation forbidden during dynamic update`

**Solutions:**
- Ensure operations happen within valid Revit transactions
- Check for concurrent modification issues

## Debugging Workflow

### Step 1: Isolate the Problem
1. Select only one element and try to send
2. If it works, gradually add more elements
3. Note which element causes the failure

### Step 2: Check Element Properties
```python
# In Revit Python Shell or Dynamo
element = doc.GetElement(ElementId(123456))
print("Name:", element.Name)
print("Category:", element.Category.Name if element.Category else "None")
print("IsValidObject:", element.IsValidObject)
print("Parameters:", [p.Definition.Name for p in element.Parameters])
```

### Step 3: Verify BREP Conversion
The enhanced logging now shows:
- `Attempting BREP conversion for X solids from element Y`
- Success/failure counts per element
- Specific error details for failures

### Step 4: Check Linked Models
If the error mentions linked models:
1. Verify linked model is loaded
2. Check permissions and access
3. Try with "Send Linked Models" disabled

## Checking Logs

### Revit Journal File
The Revit journal file contains detailed information about errors:
1. Navigate to: `%LOCALAPPDATA%\Autodesk\Revit\[Version]\Journals\`
2. Open the most recent `.txt` file
3. Search for:
   - The time when the error occurred
   - "Exception" or "Error"
   - Element IDs mentioned in the Speckle error

### Application Logs
With debug logging enabled, check:
1. Visual Studio Output window (if debugging)
2. Windows Event Viewer (for critical errors)
3. Speckle log files (if configured)

## Log Analysis

### What to Look For
1. **Element Context:**
   ```
   Conversion failed: Failed to convert Wall element 'Basic Wall' (ID: 401234)
   Element details: { ElementId: "401234", UniqueId: "xxx", Name: "Basic Wall", Category: "Walls", Type: "Wall", Document: "Project1", IsLinked: false }
   ```

2. **BREP Conversion Status:**
   ```
   Attempting BREP conversion for 3 solids from element 401234
   BREP conversion completed with 2 successes and 1 failures for element 401234
   ```

3. **Specific Exceptions:**
   ```
   InvalidOperationException during BREP conversion for element 401234
   Unexpected error during BREP conversion for element 401234. Solid info: Faces=6, Volume=2.5
   ```

## Performance Considerations

### BREP vs Mesh
- BREP provides better geometry fidelity but larger file sizes
- Mesh is faster but loses surface information
- Both are sent by default for compatibility

### Large Models
- Use selection filters to send in batches
- Monitor memory usage in Task Manager
- Consider disabling BREP for very large sends

## Getting Help

### Information to Provide
When reporting issues, include:
1. The enhanced error message
2. Element IDs that failed
3. Revit version and Speckle connector version
4. Relevant log excerpts
5. GraphQL debugger output

### Commands Summary
```bash
# Check BREP data
dotnet run --project Tools/Speckle.GraphQL.Debugger -- check-brep -p PROJECT_ID -o OBJECT_ID

# View detailed object data
dotnet run --project Tools/Speckle.GraphQL.Debugger -- object -p PROJECT_ID -o OBJECT_ID --export object_data.json

# Interactive debugging
dotnet run --project Tools/Speckle.GraphQL.Debugger -- interactive
```

## Prevention Tips

1. **Regular Testing:**
   - Test with small selections first
   - Verify BREP is working with simple geometry

2. **Model Health:**
   - Run Revit's audit tools
   - Clean up warnings in the model
   - Ensure valid geometry

3. **Settings Check:**
   - Verify conversion settings are appropriate
   - Check detail level matches model complexity
   - Ensure linked model settings are correct

---

*Last Updated: 2025-01-03*
*Related: BREP Implementation, GraphQL Debugging Tool*