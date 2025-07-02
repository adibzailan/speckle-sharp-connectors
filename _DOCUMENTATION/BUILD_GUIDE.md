# Speckle Sharp Connectors Build Guide

## Overview
This guide provides step-by-step instructions for building the Speckle Sharp Connectors, with specific focus on the BREP implementation for Revit to Rhino geometry transfer.

---

## Prerequisites

### Required Software
- **Visual Studio 2022** (Community or higher)
  - Workload: .NET desktop development
  - Component: .NET Framework 4.8 SDK
- **Revit** (2022-2026) - At least one version installed
- **Windows 10/11** (Native or VM via Parallels Desktop)

### macOS with Parallels Desktop
- Parallels Desktop 18+ with Windows 11 VM
- Minimum 8GB RAM allocated to VM
- Shared folders enabled for code access

---

## Solution Structure

### Available Solutions
| Solution File | Purpose | When to Use |
|--------------|---------|-------------|
| `Speckle.Connectors.sln` | All connectors | Full build of entire ecosystem |
| `Speckle.Revit.slnx` | Revit connector only | **Recommended for BREP work** |
| `Speckle.Revit.Local.slnx` | Revit + local SDK | When modifying SDK alongside |
| `Local.sln` | All connectors + SDK | Full local development |

### Project Organization
```
speckle-sharp-connectors/
├── Connectors/          # Host application plugins
│   └── Revit/          # Revit connector UI/commands
├── Converters/         # Geometry conversion logic
│   └── Revit/          # Revit converters (BREP here!)
│       └── Speckle.Converters.RevitShared/
│           └── ToSpeckle/Raw/Geometry/
│               └── BrepConversionToSpeckle.cs  # ← Your BREP implementation
└── DUI3/               # Desktop UI framework
```

---

## Build Steps

### Step 1: Environment Setup

#### Windows (Native)
```powershell
# Clone repository
git clone https://github.com/specklesystems/speckle-sharp-connectors.git
cd speckle-sharp-connectors
```

#### macOS with Parallels
1. **On macOS**: Clone to your Documents folder
   ```bash
   cd ~/Documents/GitHub
   git clone https://github.com/specklesystems/speckle-sharp-connectors.git
   ```

2. **In Windows VM**: Access via shared folder
   ```
   C:\Mac\Home\Documents\GitHub\speckle-sharp-connectors\
   ```

### Step 2: Open Solution in Visual Studio

1. **Launch Visual Studio 2022**
2. **File → Open → Project/Solution**
3. **Navigate to**: `C:\Mac\Home\Documents\GitHub\speckle-sharp-connectors\`
4. **Select**: `Speckle.Revit.slnx`

### Step 3: Configure Build

1. **Configuration Manager** (Build → Configuration Manager)
   - Configuration: `Release` (or `Debug` for debugging)
   - Platform: `Any CPU`
   - Check projects to build:
     - ✅ Speckle.Converters.RevitShared
     - ✅ Speckle.Converters.Revit[YourVersion]
     - ✅ Speckle.Connectors.Revit[YourVersion]

2. **Target Specific Revit Version**
   - Right-click solution → Properties
   - Startup Project: Set to your Revit version (e.g., `Speckle.Connectors.Revit2024`)

### Step 4: Restore Dependencies

#### Option A: Visual Studio UI
- Right-click solution → **Restore NuGet Packages**

#### Option B: Package Manager Console
```powershell
Update-Package -reinstall
```

#### Option C: Command Line
```powershell
dotnet restore
```

### Step 5: Build Solution

#### Option A: Visual Studio
- **Build → Build Solution** (Ctrl+Shift+B)
- Watch Output window for errors

#### Option B: Command Line
```powershell
# From solution directory
msbuild Speckle.Revit.slnx /p:Configuration=Release /p:Platform="Any CPU"

# Or using dotnet CLI
dotnet build Speckle.Revit.slnx -c Release
```

### Step 6: Verify Build Success

Check these locations for output:
```
# Converter assemblies (contains BREP logic)
Converters\Revit\Speckle.Converters.Revit2024\bin\Release\net48\
├── Speckle.Converters.Revit2024.dll
└── Speckle.Converters.RevitShared.dll  # ← Contains BrepConversionToSpeckle

# Connector assemblies (Revit plugin)
Connectors\Revit\Speckle.Connectors.Revit2024\bin\Release\net48\
├── SpeckleConnectorRevit.addin
└── Speckle.Connectors.Revit2024.dll
```

---

## Deployment

### Automatic Deployment
The build process automatically copies files to:
```
%AppData%\Autodesk\Revit\Addins\[Version]\
```

### Manual Deployment
If automatic deployment fails:
```powershell
# Example for Revit 2024
$source = "C:\Mac\Home\Documents\GitHub\speckle-sharp-connectors\Connectors\Revit\Speckle.Connectors.Revit2024\bin\Release\net48\"
$target = "$env:APPDATA\Autodesk\Revit\Addins\2024\"

Copy-Item -Path "$source\*" -Destination $target -Recurse -Force
```

---

## Troubleshooting

### Common Build Issues

#### 1. Missing BREP Types
**Error**: `The type or namespace name 'Brep' does not exist in namespace 'SOG'`

**Solutions**:
- Use `Speckle.Revit.Local.slnx` to include SDK sources
- Check if Speckle.Objects NuGet has BREP classes
- Temporarily comment out BREP-specific code to test base functionality

#### 2. Revit API Not Found
**Error**: `Could not load file or assembly 'RevitAPI'`

**Solutions**:
```xml
<!-- Check .csproj has correct Revit references -->
<PackageReference Include="Speckle.Revit.API" Version="2024.0.0" />
```

#### 3. Shared Project Issues
**Error**: `The imported project "*.projitems" was not found`

**Solution**: Ensure shared projects are loaded:
- Solution Explorer → Show All Files
- Right-click unloaded projects → Reload Project

#### 4. Access Denied During Build
**Error**: `Access to the path '*.dll' is denied`

**Solution**: 
- Close Revit if running
- Clean solution: Build → Clean Solution
- Delete `bin` and `obj` folders manually

### Build Performance Tips

1. **Disable Parallel Builds** (if issues occur)
   - Tools → Options → Projects and Solutions → Build and Run
   - Set "maximum number of parallel project builds" to 1

2. **Use Build Verbosity for Debugging**
   - Tools → Options → Projects and Solutions → Build and Run
   - Set MSBuild verbosity to "Detailed" or "Diagnostic"

3. **Incremental Builds**
   - Only modified projects rebuild automatically
   - Force rebuild: Build → Rebuild Solution

---

## Testing the BREP Implementation

### Quick Test Procedure

1. **Launch Revit**
   - Start Revit 2024 (or your version)
   - Check Speckle tab appears in ribbon

2. **Create Test Geometry**
   ```
   - Draw a simple wall
   - Create a cylindrical column
   - Model a curved wall
   ```

3. **Send via Speckle**
   - Open Speckle connector
   - Create new stream
   - Select geometry
   - Send to Speckle

4. **Verify BREP Data**
   - Check Speckle web viewer
   - Open in Rhino with Speckle connector
   - Verify smooth surfaces (not triangulated)

### Debug Configuration

For debugging BREP conversion:

1. **Set Breakpoints** in:
   - `BrepConversionToSpeckle.cs` → `Convert()` method
   - `DisplayValueExtractor.cs` → `ProcessGeometryCollections()`

2. **Debug Settings**:
   - Right-click Revit project → Properties → Debug
   - Start external program: `C:\Program Files\Autodesk\Revit 2024\Revit.exe`
   - Enable native code debugging

---

## Command Reference

### PowerShell Build Commands
```powershell
# Full clean and rebuild
.\build.ps1 clean-locks
.\build.ps1 build

# Format code before commit
.\build.ps1 format

# Run tests
.\build.ps1 test

# Create deployment package
.\build.ps1 zip
```

### Quick Build Script
Save as `build-revit.ps1`:
```powershell
param(
    [string]$Configuration = "Release",
    [string]$RevitVersion = "2024"
)

$solution = "Speckle.Revit.slnx"
$msbuild = "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"

Write-Host "Building Speckle Revit Connector..." -ForegroundColor Green
& $msbuild $solution /p:Configuration=$Configuration /p:Platform="Any CPU" /v:m

if ($LASTEXITCODE -eq 0) {
    Write-Host "Build succeeded!" -ForegroundColor Green
    Write-Host "Output location: Converters\Revit\Speckle.Converters.Revit$RevitVersion\bin\$Configuration\net48\"
} else {
    Write-Host "Build failed!" -ForegroundColor Red
}
```

---

## Next Steps

After successful build:

1. **Test BREP Conversion**
   - Start with simple geometry
   - Progress to complex surfaces
   - Monitor performance

2. **Debug if Needed**
   - Use Visual Studio debugger
   - Check logs in `%AppData%\Speckle\Logs`

3. **Contribute Back**
   - Create feature branch
   - Run formatter: `.\build.ps1 format`
   - Submit pull request

---

## Additional Resources

- [Speckle Developer Docs](https://speckle.guide/dev/)
- [Revit API Documentation](https://www.revitapidocs.com/)
- [Project CLAUDE.md](./CLAUDE.md) - AI assistant context
- [Development Log](/_DOCUMENTATION/_DEVELOPMENT_LOGS/20250702_2320-FEAT-brep_conversion_revit_to_rhino.md)

---

## Notes

- Build times: First build ~5-10 minutes, subsequent builds ~30 seconds
- Disk space: Full build requires ~2GB
- Memory: Visual Studio may use 2-4GB RAM during build
- Always run code formatter before committing: `.\build.ps1 format`