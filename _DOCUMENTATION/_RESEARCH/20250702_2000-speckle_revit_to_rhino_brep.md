# Revit to Rhino BREP Conversion Implementation Guide

## Executive Summary

This guide outlines the implementation strategy for enabling BREP (Boundary Representation) geometry transfer from Revit to Rhino through Speckle, replacing the current mesh-based approach. The solution leverages Revit's native API to extract BREP data without requiring RhinoInside.Revit, making it accessible to all Speckle users.

## Problem Statement

### Current State
- Revit geometry is currently converted to **meshes** when sent through Speckle
- Meshes appear with visible triangulation and internal edges in Rhino
- Loss of smooth surface information impacts downstream workflows
- BREP support exists for Rhino↔Rhino but not fully for Revit→Rhino

### Desired State
- Revit geometry should transfer as smooth BREP surfaces to Rhino
- Maintain exact geometric representation without triangulation
- Enable better downstream modeling and analysis workflows
- Provide mesh fallback when BREP conversion fails

## Technical Architecture

### Repository Structure
```
speckle-sharp-connectors/
├── Objects/
│   ├── Converters/
│   │   ├── ConverterRevit/
│   │   │   ├── ConverterRevitShared/
│   │   │   │   ├── ConverterRevit.cs
│   │   │   │   ├── ConversionUtils.cs
│   │   │   │   └── PartialClasses/
│   │   │   │       └── ConvertGeometry.Brep.cs (NEW)
│   │   └── ConverterRhinoGh/
│   │       └── ConverterRhinoGhShared/
│   │           └── ConverterRhinoGh.Geometry.cs
```

### Data Flow
```
Revit Element
    ↓
Revit Solid (DB.Solid)
    ↓
Extract Faces, Edges, Vertices
    ↓
Speckle BREP Object
    ↓
Send to Speckle Server
    ↓
Receive in Rhino
    ↓
Convert to Rhino BREP
```

## Implementation Details

### 1. Core BREP Converter Class

Create a new file: `ConvertGeometry.Brep.cs`

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Objects.Geometry;
using Objects.Primitive;
using Speckle.Core.Models;

namespace Objects.Converter.Revit
{
    public partial class ConverterRevit
    {
        /// <summary>
        /// Converts a Revit Solid to a Speckle BREP
        /// </summary>
        public Brep SolidToSpeckle(DB.Solid solid, string units = null)
        {
            if (solid == null || solid.Volume < 1e-6)
                return null;

            units = units ?? ModelUnits;

            var brep = new Brep
            {
                units = units,
                Surfaces = new List<Surface>(),
                Vertices = new List<Point>(),
                Curve3D = new List<ICurve>(),
                Curve2D = new List<ICurve>(),
                Edges = new List<BrepEdge>(),
                Faces = new List<BrepFace>(),
                Loops = new List<BrepLoop>(),
                Trims = new List<BrepTrim>()
            };

            // Convert vertices
            var vertexMap = ConvertVertices(solid, brep, units);

            // Convert edges and 3D curves
            var edgeMap = ConvertEdges(solid, brep, vertexMap, units);

            // Convert faces with surfaces and 2D curves
            ConvertFaces(solid, brep, edgeMap, units);

            // Set BREP properties
            brep.IsClosed = solid.Faces.Size > 0 && IsSolidClosed(solid);
            brep.volume = ScaleToSpeckle(solid.Volume, units, 3);
            brep.area = ScaleToSpeckle(solid.SurfaceArea, units, 2);

            return brep;
        }

        private Dictionary<DB.Vertex, int> ConvertVertices(
            DB.Solid solid,
            Brep brep,
            string units)
        {
            var vertexMap = new Dictionary<DB.Vertex, int>();
            int index = 0;

            foreach (DB.Vertex vertex in solid.Vertices)
            {
                var pt = vertex.Coord;
                brep.Vertices.Add(new Point(
                    ScaleToSpeckle(pt.X, units),
                    ScaleToSpeckle(pt.Y, units),
                    ScaleToSpeckle(pt.Z, units),
                    units
                ));
                vertexMap[vertex] = index++;
            }

            return vertexMap;
        }

        private Dictionary<DB.Edge, int> ConvertEdges(
            DB.Solid solid,
            Brep brep,
            Dictionary<DB.Vertex, int> vertexMap,
            string units)
        {
            var edgeMap = new Dictionary<DB.Edge, int>();
            int index = 0;

            foreach (DB.Edge edge in solid.Edges)
            {
                // Convert edge curve to Speckle
                var curve = edge.AsCurve();
                var speckleCurve = ConvertToSpeckle(curve) as ICurve;
                brep.Curve3D.Add(speckleCurve);

                // Create BREP edge
                var brepEdge = new BrepEdge
                {
                    Curve3dIndex = brep.Curve3D.Count - 1,
                    StartIndex = vertexMap[edge.GetVertex(0)],
                    EndIndex = vertexMap[edge.GetVertex(1)],
                    ProxyCurveIsReversed = edge.IsFlipped,
                    Domain = new Interval(0, 1)
                };

                brep.Edges.Add(brepEdge);
                edgeMap[edge] = index++;
            }

            return edgeMap;
        }

        private void ConvertFaces(
            DB.Solid solid,
            Brep brep,
            Dictionary<DB.Edge, int> edgeMap,
            string units)
        {
            int faceIndex = 0;

            foreach (DB.Face face in solid.Faces)
            {
                // Convert surface
                var surface = FaceToSurface(face, units);
                brep.Surfaces.Add(surface);

                var brepFace = new BrepFace
                {
                    SurfaceIndex = brep.Surfaces.Count - 1,
                    OuterLoopIndex = -1,
                    OrientationReversed = face.OrientationMatchesSurfaceOrientation
                };

                // Convert edge loops
                int loopIndex = 0;
                foreach (DB.EdgeArray edgeLoop in face.EdgeLoops)
                {
                    var brepLoop = new BrepLoop
                    {
                        FaceIndex = faceIndex,
                        Type = loopIndex == 0 ? BrepLoopType.Outer : BrepLoopType.Inner
                    };

                    if (loopIndex == 0)
                        brepFace.OuterLoopIndex = brep.Loops.Count;

                    // Convert trims
                    foreach (DB.Edge edge in edgeLoop)
                    {
                        // Get 2D parametric curve on face
                        var curve2d = ConvertEdgeTo2DCurve(face, edge, units);
                        brep.Curve2D.Add(curve2d);

                        var trim = new BrepTrim
                        {
                            EdgeIndex = edgeMap[edge],
                            FaceIndex = faceIndex,
                            LoopIndex = brep.Loops.Count,
                            CurveIndex = brep.Curve2D.Count - 1,
                            IsoStatus = BrepTrimIsoStatus.None,
                            TrimType = BrepTrimType.Boundary,
                            IsReversed = edge.IsFlipped
                        };

                        brep.Trims.Add(trim);
                    }

                    brep.Loops.Add(brepLoop);
                    loopIndex++;
                }

                brep.Faces.Add(brepFace);
                faceIndex++;
            }
        }
    }
}
```

### 2. Surface Type Conversion

```csharp
private Surface FaceToSurface(DB.Face face, string units)
{
    switch (face)
    {
        case DB.PlanarFace planarFace:
            return ConvertPlanarFace(planarFace, units);

        case DB.CylindricalFace cylFace:
            return ConvertCylindricalFace(cylFace, units);

        case DB.ConicalFace coneFace:
            return ConvertConicalFace(coneFace, units);

        case DB.RevolvedFace revFace:
            return ConvertRevolvedFace(revFace, units);

        case DB.RuledFace ruledFace:
            return ConvertRuledFace(ruledFace, units);

        case DB.HermiteFace hermiteFace:
            return ConvertHermiteFace(hermiteFace, units);

        default:
            // Fallback to NURBS surface approximation
            return ConvertToNurbsSurface(face, units);
    }
}

private Surface ConvertPlanarFace(DB.PlanarFace face, string units)
{
    var origin = PointToSpeckle(face.Origin, units);
    var normal = VectorToSpeckle(face.FaceNormal, units);
    var xDir = VectorToSpeckle(face.XVector, units);
    var yDir = VectorToSpeckle(face.YVector, units);

    // Create plane-based surface
    var plane = new Plane(origin, normal, xDir, yDir, units);

    // Get UV bounds
    var uvBox = face.GetBoundingBox();
    var domainU = new Interval(uvBox.Min.U, uvBox.Max.U);
    var domainV = new Interval(uvBox.Min.V, uvBox.Max.V);

    return new PlanarSurface
    {
        plane = plane,
        domainU = domainU,
        domainV = domainV,
        units = units
    };
}

private Surface ConvertCylindricalFace(DB.CylindricalFace face, string units)
{
    var origin = PointToSpeckle(face.Origin, units);
    var axis = VectorToSpeckle(face.Axis, units);
    var radius = ScaleToSpeckle(face.Radius, units);

    // Get parametric bounds
    var uvBox = face.GetBoundingBox();

    return new CylindricalSurface
    {
        origin = origin,
        axis = axis,
        radius = radius,
        height = ScaleToSpeckle(uvBox.Max.V - uvBox.Min.V, units),
        domainU = new Interval(uvBox.Min.U, uvBox.Max.U),
        domainV = new Interval(uvBox.Min.V, uvBox.Max.V),
        units = units
    };
}
```

### 3. Integration with Existing Converter

Update `ConverterRevit.cs` to use BREP conversion:

```csharp
public Base ConvertToSpeckle(object @object)
{
    // Existing code...

    switch (@object)
    {
        case DB.Element element:
            // Try BREP extraction first
            if (Settings.GetValueOrDefault("sendBREP", true))
            {
                var solids = GetSolidsFromElement(element);
                if (solids.Any())
                {
                    var breps = solids
                        .Select(s => SolidToSpeckle(s))
                        .Where(b => b != null)
                        .ToList();

                    if (breps.Any())
                    {
                        // Store as displayValue
                        var result = ElementToSpeckle(element);
                        result["displayValue"] = breps;
                        result["hasBREP"] = true;
                        return result;
                    }
                }
            }

            // Fallback to existing mesh conversion
            return ElementToSpeckle(element);
    }
}

private List<DB.Solid> GetSolidsFromElement(DB.Element element)
{
    var solids = new List<DB.Solid>();
    var options = new Options
    {
        ComputeReferences = true,
        DetailLevel = ViewDetailLevel.Fine
    };

    var geomElem = element.get_Geometry(options);
    if (geomElem != null)
    {
        ExtractSolids(geomElem, solids);
    }

    return solids;
}

private void ExtractSolids(GeometryElement geomElem, List<DB.Solid> solids)
{
    foreach (GeometryObject geomObj in geomElem)
    {
        if (geomObj is DB.Solid solid && solid.Volume > 0)
        {
            solids.Add(solid);
        }
        else if (geomObj is GeometryInstance instance)
        {
            ExtractSolids(instance.GetInstanceGeometry(), solids);
        }
    }
}
```

### 4. Rhino Converter Update

The existing Rhino converter already handles Speckle BREPs:

```csharp
// In ConverterRhinoGh.Geometry.cs
public RH.Brep BrepToNative(Brep brep)
{
    // Existing implementation converts Speckle BREP to Rhino BREP
    // This already works and will handle our BREPs from Revit!
}
```

## Testing Strategy

### 1. Unit Tests
```csharp
[Test]
public void CanConvertRevitWallToBREP()
{
    // Create test wall
    var wall = CreateTestWall();

    // Convert to Speckle
    var converter = new ConverterRevit();
    var result = converter.ConvertToSpeckle(wall);

    // Verify BREP exists
    Assert.IsNotNull(result["displayValue"]);
    Assert.IsTrue(result["hasBREP"]);

    var breps = result["displayValue"] as List<Brep>;
    Assert.IsNotEmpty(breps);
}
```

### 2. Test Elements
- Simple walls
- Curved walls
- Complex families
- Floors with openings
- Structural elements
- MEP elements

### 3. Performance Testing
- Measure conversion time for complex models
- Compare with mesh conversion performance
- Monitor memory usage

## Error Handling

```csharp
public Brep SafeSolidToSpeckle(DB.Solid solid, string units = null)
{
    try
    {
        return SolidToSpeckle(solid, units);
    }
    catch (Exception ex)
    {
        // Log error
        SpeckleLog.Logger.Warning(ex,
            "Failed to convert solid to BREP, falling back to mesh");

        // Return null to trigger mesh fallback
        return null;
    }
}
```

## Settings and Configuration

Add user settings for BREP conversion:

```json
{
  "sendBRE": {
    "type": "boolean",
    "default": true,
    "description": "Send geometry as BREP when possible"
  },
  "brepFallbackToMesh": {
    "type": "boolean",
    "default": true,
    "description": "Fall back to mesh when BREP conversion fails"
  },
  "brepTolerance": {
    "type": "number",
    "default": 0.001,
    "description": "Tolerance for BREP conversion (in document units)"
  }
}
```

## Performance Considerations

### 1. Caching Strategy
```csharp
private readonly Dictionary<ElementId, Brep> _brepCache = new();

public Brep GetOrConvertBREP(DB.Element element)
{
    if (_brepCache.TryGetValue(element.Id, out var cached))
        return cached;

    var brep = ConvertElementToBREP(element);
    _brepCache[element.Id] = brep;
    return brep;
}
```

### 2. Progressive Conversion
- Convert simple geometry first
- Show progress for large models
- Allow cancellation

## Limitations and Constraints

### Revit API Limitations
1. Some face types may not expose full parametric data
2. Complex sweeps/blends might require approximation
3. Rebar and reinforcement have limited solid geometry

### Known Issues
1. Very small faces might fail conversion
2. Self-intersecting geometry needs special handling
3. Non-manifold geometry requires cleanup

## Future Enhancements

1. **Optimize NURBS Surface Fitting**
   - Implement better surface fitting algorithms
   - Reduce control point count for simpler surfaces

2. **Support Additional Surface Types**
   - Swept surfaces
   - Lofted surfaces
   - Network surfaces

3. **Level of Detail Control**
   - Allow users to specify conversion accuracy
   - Provide simplified BREP option for performance

4. **Bi-directional Support**
   - Implement Rhino BREP to Revit solid conversion
   - Handle BREP editing roundtrips

## References

1. [Revit API Documentation - Solid Class](https://www.revitapidocs.com/2022/51c374d6-410f-71f2-b0bd-76b9c1e3e02f.htm)
2. [Speckle Objects Documentation](https://speckle.guide/dev/objects.html)
3. [Rhino BREP Documentation](https://developer.rhino3d.com/api/rhinocommon/rhino.geometry.brep)
4. [RhinoInside.Revit Geometry Conversion](https://github.com/mcneel/rhino.inside-revit) (reference implementation)

## Implementation Timeline

1. **Phase 1: Core Implementation** (2 weeks)
   - Basic BREP converter
   - Support for planar and cylindrical faces
   - Integration with existing converter

2. **Phase 2: Extended Surface Support** (2 weeks)
   - Additional surface types
   - Edge case handling
   - Performance optimization

3. **Phase 3: Testing and Polish** (1 week)
   - Comprehensive testing
   - Documentation
   - UI settings integration

## Conclusion

This implementation provides a robust path forward for BREP conversion from Revit to Rhino through Speckle, eliminating the need for mesh-based transfer and preserving geometric accuracy. The solution is independent of RhinoInside.Revit while learning from its proven approaches.
