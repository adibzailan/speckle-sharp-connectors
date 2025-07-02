# Speckle BREP Implementation Reference URLs

## Primary Repositories

### Speckle Repositories
- **Main Connectors Repository (Current)**: https://github.com/specklesystems/speckle-sharp-connectors
- **Legacy Repository**: https://github.com/specklesystems/speckle-sharp
- **Speckle Sharp SDK**: https://github.com/specklesystems/speckle-sharp-sdk

### RhinoInside.Revit Repository
- **Main Repository**: https://github.com/mcneel/rhino.inside-revit
- **GeometryDecoder Source**: https://github.com/mcneel/rhino.inside-revit/blob/1.x/src/RhinoInside.Revit/Convert/Geometry/GeometryDecoder.cs
- **GeometryEncoder Source**: https://github.com/mcneel/rhino.inside-revit/blob/1.x/src/RhinoInside.Revit/Convert/Geometry/GeometryEncoder.cs

## Key Source Files

### Revit Converter Files
- **ConverterRevit.cs**: https://github.com/specklesystems/speckle-sharp/blob/main/Objects/Converters/ConverterRevit/ConverterRevitShared/ConverterRevit.cs
- **ConversionUtils.cs**: https://github.com/specklesystems/speckle-sharp/blob/main/Objects/Converters/ConverterRevit/ConverterRevitShared/ConversionUtils.cs
- **ConnectorBindingsRevit.Send.cs**: https://github.com/specklesystems/speckle-sharp/blob/main/ConnectorRevit/ConnectorRevit/UI/ConnectorBindingsRevit.Send.cs

### Rhino Converter Files
- **ConverterRhinoGh.Geometry.cs**: https://github.com/specklesystems/speckle-sharp/blob/main/Objects/Converters/ConverterRhinoGh/ConverterRhinoGhShared/ConverterRhinoGh.Geometry.cs

## API Documentation

### Revit API
- **Solid Class Documentation**: https://www.revitapidocs.com/2022/51c374d6-410f-71f2-b0bd-76b9c1e3e02f.htm
- **Solid Class (APIDocs)**: https://api.apidocs.co/resolve/revit/2022/?asset_id=T:Autodesk.Revit.DB.Solid
- **Face Class**: https://www.revitapidocs.com/2022/6e91bffe-db45-d65b-9e35-45c4e5160ee8.htm
- **Edge Class**: https://www.revitapidocs.com/2022/3c190d5f-63f7-dc89-6e32-2c7e5d80ba21.htm

### Rhino API
- **Rhino BREP Documentation**: https://developer.rhino3d.com/api/rhinocommon/rhino.geometry.brep
- **RhinoCommon API**: https://developer.rhino3d.com/api/RhinoCommon/html/T_Rhino_Geometry_Brep.htm

### RhinoInside.Revit API
- **GeometryDecoder Methods**: https://www.rhino3d.com/inside/revit/api/1.0/2022/html/Methods_T_RhinoInside_Revit_Convert_Geometry_GeometryDecoder.htm
- **ToBrep(Solid) Method**: https://www.rhino3d.com/inside/revit/api/1.0/2022/html/M_RhinoInside_Revit_Convert_Geometry_GeometryDecoder_ToBrep_1.htm
- **ToBrep(Face) Method**: https://www.rhino3d.com/inside/revit/api/1.0/2022/html/M_RhinoInside_Revit_Convert_Geometry_GeometryDecoder_ToBrep.htm

## Speckle Documentation

### Official Documentation
- **Speckle Docs (Main)**: https://speckle.guide/
- **Revit Connector Docs**: https://speckle.guide/user/revit.html
- **Supported Elements**: https://speckle.guide/user/support-tables.html
- **Objects Kit Documentation**: https://speckle.guide/dev/objects.html
- **Custom Kits (Legacy)**: https://speckle.guide/dev/kits-dev.html

### Tutorials and Guides
- **Revit to Rhino Workflow**: https://www.speckle.systems/workflows/revit-to-rhino
- **Rhino to Revit Workflow**: https://www.speckle.systems/workflows/rhino-to-revit
- **Revit Elements to Rhino Tutorial**: https://www.speckle.systems/tutorials/revit-elements-to-rhino
- **Stream Rhino Geometry to Revit**: https://www.speckle.systems/tutorials/getting-rhino-geometry-into-revit
- **BREPs to Revit: DirectShapes vs FreeForm**: https://www.speckle.systems/tutorials/breps-to-revit-direct-shapes-vs-free-form-elements
- **Create Revit BIM Models in Grasshopper**: https://www.speckle.systems/tutorials/create-revit-bim-models-in-grasshopper

### Comparison Articles
- **Speckle vs Rhino.Inside.Revit**: https://www.speckle.systems/blog/speckle-vs-rhino-inside-revit-choose-the-best-tool-for-your-aec-project

## Community Discussions

### Speckle Community Forum
- **Rhino to Revit Converter - Include Data**: https://speckle.community/t/rhino-to-revit-converter-include-data/9316
- **Support for BREPs and NURBS Across Apps**: https://speckle.community/t/support-for-receiving-breps-and-nurbs-geometries-across-all-apps/4194
- **Triangulating Geometry Issue**: https://speckle.community/t/triangulating-geometry/1797
- **Could Not Convert BREP - Freeform Element**: https://speckle.community/t/could-not-convert-brep-freeform-element-revit/2034
- **Rhino to Revit with Speckle Mapper**: https://speckle.community/t/rhino-to-revit-with-speckle-mapper/6561
- **Integrating Revit Converter in C#**: https://speckle.community/t/integrating-revit-converter-in-c/1973

## GitHub Issues

### Speckle Issues
- **Display Mesh Conversions Issue #608**: https://github.com/specklesystems/speckle-sharp/issues/608
- **BREP Conversion Failure Issue #761**: https://github.com/specklesystems/speckle-sharp/issues/761

### RhinoInside.Revit Issues
- **GeometryDecoder.ToCurve Issue #531**: https://github.com/mcneel/rhino.inside-revit/issues/531
- **Brep.ToSolid() Returns None Issue #355**: https://github.com/mcneel/rhino.inside-revit/issues/355

## Additional Resources

### Releases and Downloads
- **Speckle Connectors Download**: https://www.speckle.systems/download
- **Speckle Manager**: https://speckle-releases.netlify.app/
- **Speckle Connectors Releases**: https://github.com/specklesystems/speckle-sharp-connectors/releases

### Related Projects
- **Rhino.Inside**: https://www.rhino3d.com/features/rhino-inside-revit/
- **Rhino.Inside.Revit Product Page**: https://www.rhino3d.com/rhino-inside-revit
- **Geometry Gym Rhino Inside**: https://technical.geometrygym.com/rhino-grasshopper/revit/rhino-inside-revit

### Example Code References
- **RiR C# Guide**: https://github.com/mcneel/rhino.inside-revit/blob/1.x/docs/pages/_en/beta/guides/rir-csharp.md
- **RiR Getting Started**: https://github.com/mcneel/rhino.inside-revit/blob/1.x/docs/pages/_en/1.0/getting-started.md
- **Community Code Examples**: https://gist.github.com/giobel/080894e6b850cd6c67176e3f3cd8f692

## NuGet Packages
- **Speckle.Objects**: https://www.nuget.org/packages/Speckle.Objects
- **Speckle.Objects.Converter.Rhino7**: https://www.nuget.org/packages/Speckle.Objects.Converter.Rhino7/
- **Speckle.Revit.API**: https://www.nuget.org/packages/Speckle.Revit.API

## Video Resources
- **Revit to Rhino Connection**: https://www.youtube.com/watch?v=iKf_v_P7Oek
- **Create Revit Models from Rhino/Grasshopper**: https://www.youtube.com/watch?v=YtcbPOkgs1A
- **Getting Started with Speckle Revit**: https://www.youtube.com/watch?v=ijNFzRRRBpg
- **Intro to C# Coding with Speckle**: https://www.youtube.com/watch?v=W4mGRu_I2Ag

---

## Quick Reference for Implementation

### Critical Files to Modify
1. `ConverterRevit.cs` - Main converter logic
2. `ConversionUtils.cs` - Helper methods
3. Create new: `ConvertGeometry.Brep.cs` - BREP conversion implementation

### Key Methods to Reference
1. RiR's `GeometryDecoder.ToBrep(Solid)`
2. Speckle's `BrepToNative(Brep)` in Rhino converter
3. Existing `GetElementMesh()` for fallback

### Testing Resources
1. Revit Basic Sample Project (included with Revit)
2. Speckle test models from tutorials
3. Complex geometry test cases from community issues
