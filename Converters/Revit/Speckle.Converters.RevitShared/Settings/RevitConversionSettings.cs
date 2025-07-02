namespace Speckle.Converters.RevitShared.Settings;

public record RevitConversionSettings(
  DB.Document Document,
  DetailLevelType DetailLevel,
  DB.Transform? ReferencePointTransform,
  string SpeckleUnits,
  bool SendParameterNullOrEmptyStrings,
  bool SendLinkedModels,
  bool SendRebarsAsVolumetric,
  bool SendAsBREP = true,
  bool BREPFallbackToMesh = true,
  double BREPTolerance = 0.001, // In document units
  double Tolerance = 0.0164042 // 5mm in ft
);
