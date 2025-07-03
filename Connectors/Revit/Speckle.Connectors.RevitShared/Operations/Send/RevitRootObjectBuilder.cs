using Autodesk.Revit.DB;
using Microsoft.Extensions.Logging;
using Speckle.Connectors.Common.Builders;
using Speckle.Connectors.Common.Caching;
using Speckle.Connectors.Common.Conversion;
using Speckle.Connectors.Common.Operations;
using Speckle.Connectors.Common.Threading;
using Speckle.Connectors.DUI.Exceptions;
using Speckle.Connectors.Revit.HostApp;
using Speckle.Converters.Common;
using Speckle.Converters.RevitShared.Helpers;
using Speckle.Converters.RevitShared.Settings;
using Speckle.Sdk;
using Speckle.Sdk.Common;
using Speckle.Sdk.Models;
using Speckle.Sdk.Models.Collections;

namespace Speckle.Connectors.Revit.Operations.Send;

#pragma warning disable CA1502 // Avoid excessive complexity
#pragma warning disable CA1506 // Avoid excessive class coupling
public class RevitRootObjectBuilder(
  IRootToSpeckleConverter converter,
  IConverterSettingsStore<RevitConversionSettings> converterSettings,
  ISendConversionCache sendConversionCache,
  ElementUnpacker elementUnpacker,
  LevelUnpacker levelUnpacker,
  IThreadContext threadContext,
  SendCollectionManager sendCollectionManager,
  ILogger<RevitRootObjectBuilder> logger,
  RevitToSpeckleCacheSingleton revitToSpeckleCacheSingleton,
  LinkedModelHandler linkedModelHandler
) : IRootObjectBuilder<DocumentToConvert>
{
  public Task<RootObjectBuilderResult> Build(
    IReadOnlyList<DocumentToConvert> documentElementContexts,
    SendInfo sendInfo,
    IProgress<CardProgress> onOperationProgressed,
    CancellationToken ct = default
  ) =>
    threadContext.RunOnMainAsync(
      () => Task.FromResult(BuildSync(documentElementContexts, sendInfo, onOperationProgressed, ct))
    );

  private RootObjectBuilderResult BuildSync(
    IReadOnlyList<DocumentToConvert> documentElementContexts,
    SendInfo sendInfo,
    IProgress<CardProgress> onOperationProgressed,
    CancellationToken cancellationToken
  )
  {
    var doc = converterSettings.Current.Document;

    if (doc.IsFamilyDocument)
    {
      throw new SpeckleException("Family Environment documents are not supported.");
    }

    // init the root
    Collection rootObject =
      new() { name = converterSettings.Current.Document.PathName.Split('\\').Last().Split('.').First() };
    rootObject["units"] = converterSettings.Current.SpeckleUnits;

    var filteredDocumentsToConvert = new List<DocumentToConvert>();
    bool sendWithLinkedModels = converterSettings.Current.SendLinkedModels;
    List<SendConversionResult> results = new();

    // Prepare linked model display names if needed
    if (sendWithLinkedModels)
    {
      linkedModelHandler.PrepareLinkedModelNames(documentElementContexts);
    }

    foreach (var documentElementContext in documentElementContexts)
    {
      // add appropriate warnings for linked documents
      if (documentElementContext.Doc.IsLinked && !sendWithLinkedModels)
      {
        results.Add(
          new(
            Status.WARNING,
            documentElementContext.Doc.PathName,
            typeof(RevitLinkInstance).ToString(),
            null,
            new SpeckleException("Enable linked model support from the settings to send this object")
          )
        );
        continue;
      }

      // filter for valid elements
      // if send linked models setting is disabled List<Elements> will be empty, and we won't enter foreach loop
      var elementsInTransform = new List<Element>();
      foreach (var el in documentElementContext.Elements)
      {
        if (el == null || el.Category == null)
        {
          continue;
        }
        elementsInTransform.Add(el);
      }

      // only add contexts with elements
      if (elementsInTransform.Count > 0)
      {
        filteredDocumentsToConvert.Add(documentElementContext with { Elements = elementsInTransform });
      }
    }

    // TODO: check the exception!!!!
    if (filteredDocumentsToConvert.Count == 0)
    {
      throw new SpeckleSendFilterException("No objects were found. Please update your publish filter!");
    }

    // Unpack groups (& other complex data structures)
    var atomicObjectsByDocumentAndTransform = new List<DocumentToConvert>();
    var atomicObjectCount = 0;
    foreach (var filteredDocumentToConvert in filteredDocumentsToConvert)
    {
      using (
        converterSettings.Push(currentSettings => currentSettings with { Document = filteredDocumentToConvert.Doc })
      )
      {
        var atomicObjects = elementUnpacker
          .UnpackSelectionForConversion(filteredDocumentToConvert.Elements, filteredDocumentToConvert.Doc)
          .ToList();
        atomicObjectsByDocumentAndTransform.Add(filteredDocumentToConvert with { Elements = atomicObjects });
        atomicObjectCount += atomicObjects.Count;
      }
    }

    var countProgress = 0;
    var cacheHitCount = 0;
    var skippedObjectCount = 0;

    foreach (var atomicObjectByDocumentAndTransform in atomicObjectsByDocumentAndTransform)
    {
      string? modelDisplayName = null;
      if (atomicObjectByDocumentAndTransform.Doc.IsLinked)
      {
        string id = linkedModelHandler.GetIdFromDocumentToConvert(atomicObjectByDocumentAndTransform);
        linkedModelHandler.LinkedModelDisplayNames.TryGetValue(id, out modelDisplayName);
      }

      // here we do magic for changing the transform and the related document according to model. first one is always the main model.
      using (
        converterSettings.Push(currentSettings =>
          currentSettings with
          {
            ReferencePointTransform = atomicObjectByDocumentAndTransform.Transform,
            Document = atomicObjectByDocumentAndTransform.Doc,
          }
        )
      )
      {
        var atomicObjects = atomicObjectByDocumentAndTransform.Elements;
        foreach (Element revitElement in atomicObjects)
        {
          cancellationToken.ThrowIfCancellationRequested();
          string applicationId = revitElement.UniqueId;
          string sourceType = revitElement.GetType().Name;
          try
          {
            if (!SupportedCategoriesUtils.IsSupportedCategory(revitElement.Category))
            {
              var cat = revitElement.Category != null ? revitElement.Category.Name : "No category";
              results.Add(
                new(
                  Status.WARNING,
                  revitElement.UniqueId,
                  cat,
                  null,
                  new SpeckleException($"Category {cat} is not supported.")
                )
              );
              skippedObjectCount++;
              continue;
            }

            Base converted;
            bool hasTransform = atomicObjectByDocumentAndTransform.Transform != null;

            // non-transformed elements can safely rely on cache
            // TODO: Potential here to transform cached objects and NOT reconvert,
            // TODO: we wont do !hasTransform here, and re-set application id before this
            if (
              !hasTransform
              && sendConversionCache.TryGetValue(sendInfo.ProjectId, applicationId, out ObjectReference? value)
            )
            {
              converted = value;
              cacheHitCount++;
            }
            // not in cache means we convert
            else
            {
              // if it has a transform we append transform hash to the applicationId to distinguish the elements from other instances
              if (hasTransform)
              {
                string transformHash = linkedModelHandler.GetTransformHash(
                  atomicObjectByDocumentAndTransform.Transform.NotNull()
                );
                applicationId = $"{applicationId}_t{transformHash}";
              }
              // normal conversions
              converted = converter.Convert(revitElement);
              converted.applicationId = applicationId;
            }

            var collection = sendCollectionManager.GetAndCreateObjectHostCollection(
              revitElement,
              rootObject,
              sendWithLinkedModels,
              modelDisplayName
            );

            collection.elements.Add(converted);
            results.Add(new(Status.SUCCESS, applicationId, sourceType, converted));
          }
          catch (Exception ex) when (!ex.IsFatal())
          {
            // Gather element context for better error reporting
            var elementInfo = new
            {
              ElementId = revitElement.Id.ToString(),
              UniqueId = revitElement.UniqueId,
              Name = revitElement.Name,
              Category = revitElement.Category?.Name ?? "No category",
              Type = sourceType,
              Document = revitElement.Document.Title,
              IsLinked = atomicObjectByDocumentAndTransform.Doc.IsLinked
            };
            
            var contextualMessage = $"Failed to convert {elementInfo.Category} element '{elementInfo.Name}' (ID: {elementInfo.ElementId}){(elementInfo.IsLinked ? " from linked model" : "")}";
            
            logger.LogError(ex, "Conversion failed: {ContextualMessage}. Element details: {@ElementInfo}", contextualMessage, elementInfo);
            
            // Create a more informative exception for the results
            var detailedException = new SpeckleException(
              $"{contextualMessage}: {ex.Message}",
              ex
            );
            
            results.Add(new(Status.ERROR, applicationId, sourceType, null, detailedException));
          }

          onOperationProgressed.Report(new("Converting", (double)++countProgress / atomicObjectCount));
        }
      }
    }

    if (results.All(x => x.Status == Status.ERROR) || skippedObjectCount == atomicObjectCount)
    {
      // Create detailed error summary
      var errorSummary = new System.Text.StringBuilder();
      errorSummary.AppendLine($"Failed to convert all {atomicObjectCount} objects.");
      errorSummary.AppendLine($"Errors: {results.Count(r => r.Status == Status.ERROR)}, Skipped: {skippedObjectCount}");
      
      // Group errors by error message pattern
      var errorGroups = results
        .Where(r => r.Status == Status.ERROR && r.Error != null && r.Error.Message != null)
        .GroupBy(r => GetErrorMessageCategory(r.Error!.Message))
        .OrderByDescending(g => g.Count());
      
      errorSummary.AppendLine("\nError Summary:");
      foreach (var group in errorGroups.Take(5)) // Show top 5 error types
      {
        errorSummary.AppendLine($"  - {group.Key}: {group.Count()} occurrences");
      }
      
      // Show first few detailed errors
      errorSummary.AppendLine("\nFirst 3 errors:");
      var detailedErrors = results
        .Where(r => r.Status == Status.ERROR && r.Error != null && r.Error.Message != null)
        .Take(3);
      
      foreach (var error in detailedErrors)
      {
        errorSummary.AppendLine($"  - {error.SourceType} ({error.SourceId}): {error.Error!.Message}");
      }
      
      // Log all errors for debugging
      logger.LogError("Conversion failed completely. Full error details: {@Results}", 
        results.Where(r => r.Status == Status.ERROR).ToList());
      
      throw new SpeckleException(errorSummary.ToString());
    }

    var flatElements = atomicObjectsByDocumentAndTransform.SelectMany(t => t.Elements).ToList();
    var idsAndSubElementIds = elementUnpacker.GetElementsAndSubelementIdsFromAtomicObjects(flatElements);

    var renderMaterialProxies = revitToSpeckleCacheSingleton.GetRenderMaterialProxyListForObjects(idsAndSubElementIds);
    rootObject[ProxyKeys.RENDER_MATERIAL] = renderMaterialProxies;

    var levelProxies = levelUnpacker.Unpack(flatElements);
    rootObject[ProxyKeys.LEVEL] = levelProxies;

    // NOTE: these are currently not used anywhere, we'll skip them until someone calls for it back
    // rootObject[ProxyKeys.PARAMETER_DEFINITIONS] = _parameterDefinitionHandler.Definitions;

    // we want to store transform data for chosen reference point setting
    if (converterSettings.Current.ReferencePointTransform is Transform transform)
    {
      var transformMatrix = ReferencePointHelper.CreateTransformDataForRootObject(transform);
      rootObject[ReferencePointHelper.REFERENCE_POINT_TRANSFORM_KEY] = transformMatrix;
    }

    return new RootObjectBuilderResult(rootObject, results);
  }
  
  private static string GetErrorMessageCategory(string errorMessage)
  {
    // Categorize common error patterns using IndexOf for .NET Framework compatibility
    if (errorMessage.IndexOf("BREP", StringComparison.OrdinalIgnoreCase) >= 0)
    {
      return "BREP Conversion Error";
    }
    if (errorMessage.IndexOf("geometry", StringComparison.OrdinalIgnoreCase) >= 0)
    {
      return "Geometry Error";
    }
    if (errorMessage.IndexOf("parameter", StringComparison.OrdinalIgnoreCase) >= 0)
    {
      return "Parameter Error";
    }
    if (errorMessage.IndexOf("deleted", StringComparison.OrdinalIgnoreCase) >= 0)
    {
      return "Deleted Element";
    }
    if (errorMessage.IndexOf("invalid", StringComparison.OrdinalIgnoreCase) >= 0)
    {
      return "Invalid Element";
    }
    if (errorMessage.IndexOf("null", StringComparison.OrdinalIgnoreCase) >= 0)
    {
      return "Null Reference";
    }
    if (errorMessage.IndexOf("category", StringComparison.OrdinalIgnoreCase) >= 0)
    {
      return "Unsupported Category";
    }
    
    // Return first 50 chars of message as category if no pattern matches
#pragma warning disable IDE0057 // Substring can be simplified - not available in .NET Framework
    return errorMessage.Length > 50 ? errorMessage.Substring(0, 50) + "..." : errorMessage;
#pragma warning restore IDE0057
  }
}
#pragma warning restore CA1502
#pragma warning restore CA1506
