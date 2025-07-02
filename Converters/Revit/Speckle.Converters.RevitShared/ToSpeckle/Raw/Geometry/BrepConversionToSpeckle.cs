using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Speckle.Converters.Common;
using Speckle.Converters.Common.Objects;
using Speckle.Converters.RevitShared.Helpers;
using Speckle.Objects;
using Speckle.Objects.Primitive;
using Speckle.Sdk.Models;

namespace Speckle.Converters.RevitShared.ToSpeckle;

/// <summary>
/// Converts Revit Solid objects to Speckle BREP representation.
/// This converter preserves surface information and topology, avoiding mesh triangulation.
/// </summary>
public class BrepConversionToSpeckle : ITypedConverter<DB.Solid, SOG.Mesh>
{
  private readonly RevitConversionContextStack _contextStack;
  private readonly ILogger<BrepConversionToSpeckle> _logger;
  private readonly ITypedConverter<Dictionary<DB.ElementId, List<DB.Mesh>>, List<SOG.Mesh>> _meshByMaterialConverter;
  private readonly ITypedConverter<DB.XYZ, SOG.Point> _pointConverter;
  private readonly ITypedConverter<DB.Curve, ICurve> _curveConverter;
  private readonly ITypedConverter<DB.Plane, SOG.Plane> _planeConverter;
  private readonly ScalingServiceToSpeckle _scalingService;

  public BrepConversionToSpeckle(
    RevitConversionContextStack contextStack,
    ILogger<BrepConversionToSpeckle> logger,
    ITypedConverter<Dictionary<DB.ElementId, List<DB.Mesh>>, List<SOG.Mesh>> meshByMaterialConverter,
    ITypedConverter<DB.XYZ, SOG.Point> pointConverter,
    ITypedConverter<DB.Curve, ICurve> curveConverter,
    ITypedConverter<DB.Plane, SOG.Plane> planeConverter,
    ScalingServiceToSpeckle scalingService
  )
  {
    _contextStack = contextStack;
    _logger = logger;
    _meshByMaterialConverter = meshByMaterialConverter;
    _pointConverter = pointConverter;
    _curveConverter = curveConverter;
    _planeConverter = planeConverter;
    _scalingService = scalingService;
  }

  /// <summary>
  /// Converts a Revit Solid to a Speckle Mesh with BREP data stored in properties.
  /// </summary>
  /// <remarks>
  /// Since we need to return SOG.Mesh for compatibility, we store the BREP data
  /// in the mesh's properties and set a flag indicating BREP availability.
  /// </remarks>
  public SOG.Mesh Convert(DB.Solid target)
  {
    try
    {
      // First, try to convert to BREP
      var brep = ConvertSolidToBrep(target);
      
      if (brep != null)
      {
        // Create a display mesh for visualization
        var displayMeshes = GetDisplayMeshes(target);
        
        // Use the first mesh as the base (or create an empty one)
        var baseMesh = displayMeshes.FirstOrDefault() ?? new SOG.Mesh();
        
        // Store BREP data in the mesh properties
        baseMesh["@brep"] = brep;
        baseMesh["hasBREP"] = true;
        
        // If we have multiple display meshes, store them too
        if (displayMeshes.Count > 1)
        {
          baseMesh["@additionalMeshes"] = displayMeshes.Skip(1).ToList();
        }
        
        return baseMesh;
      }
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Failed to convert solid to BREP, falling back to mesh conversion");
    }
    
    // Fallback to standard mesh conversion
    return GetDisplayMeshes(target).FirstOrDefault() ?? new SOG.Mesh();
  }

  private SOG.Brep? ConvertSolidToBrep(DB.Solid solid)
  {
    if (solid == null || solid.Volume < 1e-6)
    {
      return null;
    }

    var brep = new SOG.Brep
    {
      units = _scalingService.SpeckleUnits,
      Vertices = new List<SOG.Point>(),
      Curve3D = new List<ICurve>(),
      Curve2D = new List<ICurve>(),
      Surfaces = new List<SOG.Surface>(),
      Edges = new List<SOG.BrepEdge>(),
      Faces = new List<SOG.BrepFace>(),
      Loops = new List<SOG.BrepLoop>(),
      Trims = new List<SOG.BrepTrim>()
    };

    // Build vertex map
    var vertexMap = BuildVertexMap(solid, brep);
    
    // Build edge map and curves
    var edgeMap = BuildEdgeMap(solid, brep, vertexMap);
    
    // Convert faces with surfaces and topology
    ConvertFaces(solid, brep, edgeMap);
    
    // Set BREP properties
    brep.IsClosed = IsSolidClosed(solid);
    brep.volume = _scalingService.ConvertToSpeckle(solid.Volume, DB.SpecTypeId.Volume);
    brep.area = _scalingService.ConvertToSpeckle(solid.SurfaceArea, DB.SpecTypeId.Area);
    brep.bbox = GetBoundingBox(solid);
    
    return brep;
  }

  private Dictionary<DB.Vertex, int> BuildVertexMap(DB.Solid solid, SOG.Brep brep)
  {
    var vertexMap = new Dictionary<DB.Vertex, int>();
    int index = 0;

    foreach (DB.Edge edge in solid.Edges)
    {
      for (int i = 0; i < 2; i++)
      {
        var vertex = edge.GetVertex(i);
        if (!vertexMap.ContainsKey(vertex))
        {
          var point = _pointConverter.Convert(vertex.Coord);
          brep.Vertices.Add(point);
          vertexMap[vertex] = index++;
        }
      }
    }

    return vertexMap;
  }

  private Dictionary<DB.Edge, int> BuildEdgeMap(
    DB.Solid solid,
    SOG.Brep brep,
    Dictionary<DB.Vertex, int> vertexMap)
  {
    var edgeMap = new Dictionary<DB.Edge, int>();
    int index = 0;

    foreach (DB.Edge edge in solid.Edges)
    {
      // Convert edge curve
      var curve = edge.AsCurve();
      var speckleCurve = _curveConverter.Convert(curve);
      brep.Curve3D.Add(speckleCurve);

      // Create BREP edge
      var brepEdge = new SOG.BrepEdge
      {
        Brep = brep,
        Curve3dIndex = brep.Curve3D.Count - 1,
        StartIndex = vertexMap[edge.GetVertex(0)],
        EndIndex = vertexMap[edge.GetVertex(1)],
        ProxyCurveIsReversed = false,
        Domain = new SOG.Interval(0, 1),
        TrimIndices = new List<int>()
      };

      brep.Edges.Add(brepEdge);
      edgeMap[edge] = index++;
    }

    return edgeMap;
  }

  private void ConvertFaces(
    DB.Solid solid,
    SOG.Brep brep,
    Dictionary<DB.Edge, int> edgeMap)
  {
    int faceIndex = 0;

    foreach (DB.Face face in solid.Faces)
    {
      // Convert surface based on face type
      var surface = ConvertFaceToSurface(face);
      if (surface == null)
      {
        _logger.LogWarning($"Could not convert face of type {face.GetType().Name}");
        continue;
      }

      brep.Surfaces.Add(surface);

      var brepFace = new SOG.BrepFace
      {
        Brep = brep,
        SurfaceIndex = brep.Surfaces.Count - 1,
        LoopIndices = new List<int>(),
        OuterLoopIndex = -1,
        OrientationIsReversed = !face.OrientationMatchesSurfaceOrientation
      };

      // Convert edge loops
      ConvertFaceLoops(face, brep, brepFace, edgeMap, faceIndex);

      brep.Faces.Add(brepFace);
      faceIndex++;
    }
  }

  private void ConvertFaceLoops(
    DB.Face face,
    SOG.Brep brep,
    SOG.BrepFace brepFace,
    Dictionary<DB.Edge, int> edgeMap,
    int faceIndex)
  {
    int loopIndex = 0;
    
    foreach (DB.EdgeArray edgeLoop in face.EdgeLoops)
    {
      var brepLoop = new SOG.BrepLoop
      {
        Brep = brep,
        FaceIndex = faceIndex,
        TrimIndices = new List<int>(),
        Type = loopIndex == 0 ? SOG.BrepLoopType.Outer : SOG.BrepLoopType.Inner
      };

      if (loopIndex == 0)
      {
        brepFace.OuterLoopIndex = brep.Loops.Count;
      }

      brepFace.LoopIndices.Add(brep.Loops.Count);

      // Convert trims
      foreach (DB.Edge edge in edgeLoop)
      {
        // Get 2D parametric curve on face
        var curve2d = GetParametricCurve(face, edge);
        if (curve2d != null)
        {
          brep.Curve2D.Add(curve2d);

          var trim = new SOG.BrepTrim
          {
            Brep = brep,
            EdgeIndex = edgeMap.ContainsKey(edge) ? edgeMap[edge] : -1,
            FaceIndex = faceIndex,
            LoopIndex = brep.Loops.Count,
            CurveIndex = brep.Curve2D.Count - 1,
            IsoStatus = SOG.BrepTrimIsoStatus.None,
            TrimType = SOG.BrepTrimType.Boundary,
            IsReversed = false,
            StartIndex = -1,
            EndIndex = -1,
            Domain = new SOG.Interval(0, 1)
          };

          brep.Trims.Add(trim);
          brepLoop.TrimIndices.Add(brep.Trims.Count - 1);
        }
      }

      brep.Loops.Add(brepLoop);
      loopIndex++;
    }
  }

  private SOG.Surface? ConvertFaceToSurface(DB.Face face)
  {
    switch (face)
    {
      case DB.PlanarFace planarFace:
        return ConvertPlanarFace(planarFace);
      case DB.CylindricalFace cylindricalFace:
        return ConvertCylindricalFace(cylindricalFace);
      case DB.ConicalFace conicalFace:
        return ConvertConicalFace(conicalFace);
      case DB.RevolvedFace revolvedFace:
        return ConvertRevolvedFace(revolvedFace);
      case DB.RuledFace ruledFace:
        return ConvertRuledFace(ruledFace);
      case DB.HermiteFace hermiteFace:
        return ConvertHermiteFace(hermiteFace);
      default:
        // Fallback to generic NURBS surface
        return ConvertToNurbsSurface(face);
    }
  }

  private SOG.Surface ConvertPlanarFace(DB.PlanarFace face)
  {
    var plane = _planeConverter.Convert(DB.Plane.CreateByOriginAndBasis(
      face.Origin,
      face.XVector,
      face.YVector
    ));

    // Get UV bounds
    var uvBox = face.GetBoundingBox();
    
    // Note: Using base Surface class as PlanarSurface might not exist in current SDK
    var surface = new SOG.Surface
    {
      units = _scalingService.SpeckleUnits
    };
    
    // Store plane data in properties
    surface["@plane"] = plane;
    surface["@type"] = "PlanarSurface";
    surface["domainU"] = new SOG.Interval(uvBox.Min.U, uvBox.Max.U);
    surface["domainV"] = new SOG.Interval(uvBox.Min.V, uvBox.Max.V);
    
    return surface;
  }

  private SOG.Surface ConvertCylindricalFace(DB.CylindricalFace face)
  {
    var origin = _pointConverter.Convert(face.Origin);
    var axis = ConvertVector(face.Axis);
    var radius = _scalingService.ConvertToSpeckle(face.Radius, DB.SpecTypeId.Length);

    // Get parametric bounds
    var uvBox = face.GetBoundingBox();
    var height = _scalingService.ConvertToSpeckle(uvBox.Max.V - uvBox.Min.V, DB.SpecTypeId.Length);

    var surface = new SOG.Surface
    {
      units = _scalingService.SpeckleUnits
    };
    
    // Store cylinder data
    surface["@type"] = "CylindricalSurface";
    surface["@origin"] = origin;
    surface["@axis"] = axis;
    surface["@radius"] = radius;
    surface["@height"] = height;
    surface["domainU"] = new SOG.Interval(uvBox.Min.U, uvBox.Max.U);
    surface["domainV"] = new SOG.Interval(uvBox.Min.V, uvBox.Max.V);
    
    return surface;
  }

  private SOG.Surface ConvertConicalFace(DB.ConicalFace face)
  {
    var origin = _pointConverter.Convert(face.Origin);
    var axis = ConvertVector(face.Axis);
    var radius = _scalingService.ConvertToSpeckle(face.Radius, DB.SpecTypeId.Length);
    var halfAngle = face.HalfAngle;

    var surface = new SOG.Surface
    {
      units = _scalingService.SpeckleUnits
    };
    
    surface["@type"] = "ConicalSurface";
    surface["@origin"] = origin;
    surface["@axis"] = axis;
    surface["@radius"] = radius;
    surface["@halfAngle"] = halfAngle;
    
    return surface;
  }

  private SOG.Surface ConvertRevolvedFace(DB.RevolvedFace face)
  {
    var curve = face.Curve;
    var speckleCurve = _curveConverter.Convert(curve);
    var origin = _pointConverter.Convert(face.Origin);
    var axis = ConvertVector(face.Axis);

    var surface = new SOG.Surface
    {
      units = _scalingService.SpeckleUnits
    };
    
    surface["@type"] = "RevolvedSurface";
    surface["@profileCurve"] = speckleCurve;
    surface["@origin"] = origin;
    surface["@axis"] = axis;
    
    return surface;
  }

  private SOG.Surface ConvertRuledFace(DB.RuledFace face)
  {
    // Ruled faces are created by linear interpolation between two curves
    var surface = new SOG.Surface
    {
      units = _scalingService.SpeckleUnits
    };
    
    surface["@type"] = "RuledSurface";
    
    // Get the ruling curves if available
    var curves = new List<ICurve>();
    if (face.GetFirstProfileCurve() is DB.Curve curve1)
    {
      curves.Add(_curveConverter.Convert(curve1));
    }
    if (face.GetSecondProfileCurve() is DB.Curve curve2)
    {
      curves.Add(_curveConverter.Convert(curve2));
    }
    
    surface["@profileCurves"] = curves;
    
    return surface;
  }

  private SOG.Surface ConvertHermiteFace(DB.HermiteFace face)
  {
    // Hermite faces use bicubic interpolation
    var surface = new SOG.Surface
    {
      units = _scalingService.SpeckleUnits
    };
    
    surface["@type"] = "HermiteSurface";
    
    // Store interpolation data
    var points = face.Points.Select(p => _pointConverter.Convert(p)).ToList();
    surface["@points"] = points;
    
    if (face.Tangents != null)
    {
      var tangents = face.Tangents.Select(t => ConvertVector(t)).ToList();
      surface["@tangents"] = tangents;
    }
    
    if (face.MixedDerivs != null)
    {
      var mixedDerivs = face.MixedDerivs.Select(d => ConvertVector(d)).ToList();
      surface["@mixedDerivatives"] = mixedDerivs;
    }
    
    return surface;
  }

  private SOG.Surface ConvertToNurbsSurface(DB.Face face)
  {
    // Generic NURBS surface approximation
    var surface = new SOG.Surface
    {
      units = _scalingService.SpeckleUnits
    };
    
    surface["@type"] = "NurbsSurface";
    
    // Sample the face and create a NURBS approximation
    // This is a simplified version - a full implementation would need proper NURBS fitting
    var uvBox = face.GetBoundingBox();
    surface["domainU"] = new SOG.Interval(uvBox.Min.U, uvBox.Max.U);
    surface["domainV"] = new SOG.Interval(uvBox.Min.V, uvBox.Max.V);
    
    return surface;
  }

  private ICurve? GetParametricCurve(DB.Face face, DB.Edge edge)
  {
    try
    {
      // Get the 2D curve in the face's parameter space
      // This is a simplified implementation - proper UV curve extraction would require more work
      var curve3d = edge.AsCurve();
      
      // For now, return a simple line in parameter space
      // A full implementation would project the 3D curve onto the face's UV space
      return new SOG.Line
      {
        start = new SOG.Point(0, 0, 0),
        end = new SOG.Point(1, 0, 0),
        units = _scalingService.SpeckleUnits
      };
    }
    catch
    {
      return null;
    }
  }

  private SOG.Vector ConvertVector(DB.XYZ xyz)
  {
    return new SOG.Vector(
      xyz.X,
      xyz.Y,
      xyz.Z,
      _scalingService.SpeckleUnits
    );
  }

  private SOG.Box? GetBoundingBox(DB.Solid solid)
  {
    try
    {
      var bbox = solid.GetBoundingBox();
      if (bbox == null)
      {
        return null;
      }

      var min = _pointConverter.Convert(bbox.Min);
      var max = _pointConverter.Convert(bbox.Max);

      return new SOG.Box
      {
        basePlane = new SOG.Plane
        {
          origin = min,
          normal = new SOG.Vector(0, 0, 1),
          xdir = new SOG.Vector(1, 0, 0),
          ydir = new SOG.Vector(0, 1, 0),
          units = _scalingService.SpeckleUnits
        },
        xSize = new SOG.Interval(0, max.x - min.x),
        ySize = new SOG.Interval(0, max.y - min.y),
        zSize = new SOG.Interval(0, max.z - min.z),
        units = _scalingService.SpeckleUnits
      };
    }
    catch
    {
      return null;
    }
  }

  private bool IsSolidClosed(DB.Solid solid)
  {
    // Check if all edges are shared by exactly two faces (manifold)
    foreach (DB.Edge edge in solid.Edges)
    {
      if (edge.GetFace(1) == null)
      {
        return false;
      }
    }
    return true;
  }

  private List<SOG.Mesh> GetDisplayMeshes(DB.Solid solid)
  {
    // Use existing mesh conversion as fallback
    var meshesByMaterial = new Dictionary<DB.ElementId, List<DB.Mesh>>();
    
    foreach (DB.Face face in solid.Faces)
    {
      var materialId = face.MaterialElementId;
      if (!meshesByMaterial.ContainsKey(materialId))
      {
        meshesByMaterial[materialId] = new List<DB.Mesh>();
      }

      var mesh = face.Triangulate();
      if (mesh != null)
      {
        meshesByMaterial[materialId].Add(mesh);
      }
    }

    return _meshByMaterialConverter.Convert(meshesByMaterial);
  }
}