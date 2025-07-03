using Microsoft.Extensions.Logging;
using Speckle.Connectors.DUI.Bindings;
using Speckle.Connectors.DUI.Logging;
using Speckle.Sdk;
using Speckle.Sdk.Common;

namespace Speckle.Connectors.Revit.Plugin;

#pragma warning disable CA1032
public class SpeckleRevitTaskException(Exception exception) : SpeckleException(GetDetailedMessage(exception), exception)
#pragma warning restore CA1032
{
  private static string GetDetailedMessage(Exception ex)
  {
    // Check for Revit-specific exceptions first
    var typeName = ex.GetType().FullName ?? ex.GetType().Name;
    
    // Handle common Revit exceptions with specific messages
    return typeName switch
    {
      "Autodesk.Revit.Exceptions.InvalidOperationException" => $"Invalid Revit operation: {ex.Message}",
      "Autodesk.Revit.Exceptions.ArgumentException" => $"Invalid argument in Revit API: {ex.Message}",
      "Autodesk.Revit.Exceptions.ArgumentNullException" => $"Null argument in Revit API: {ex.Message}",
      "Autodesk.Revit.Exceptions.ArgumentOutOfRangeException" => $"Argument out of range: {ex.Message}",
      "Autodesk.Revit.Exceptions.InvalidObjectException" => $"Invalid Revit object: {ex.Message}",
      "Autodesk.Revit.Exceptions.ElementDeletedException" => $"Element has been deleted: {ex.Message}",
      "Autodesk.Revit.Exceptions.InternalException" => $"Internal Revit error: {ex.Message}",
      "Autodesk.Revit.Exceptions.ExternalApplicationException" => $"External application error: {ex.Message}",
      "Autodesk.Revit.Exceptions.ApplicationException" => $"Revit application error: {ex.Message}",
      "Autodesk.Revit.Exceptions.ForbiddenForDynamicUpdateException" => $"Operation forbidden during dynamic update: {ex.Message}",
      "Autodesk.Revit.Exceptions.RegenerationFailedException" => $"Regeneration failed: {ex.Message}",
      "Autodesk.Revit.Exceptions.CentralModelException" => $"Central model error: {ex.Message}",
      "Autodesk.Revit.Exceptions.FileAccessException" => $"File access error: {ex.Message}",
      "Autodesk.Revit.Exceptions.FileNotFoundException" => $"File not found: {ex.Message}",
      "Autodesk.Revit.Exceptions.CannotOpenBothCentralAndLocalException" => $"Cannot open both central and local: {ex.Message}",
      "Autodesk.Revit.Exceptions.CorruptModelException" => $"Model is corrupt: {ex.Message}",
      "Autodesk.Revit.Exceptions.InvalidModelException" => $"Invalid model: {ex.Message}",
      "Autodesk.Revit.Exceptions.WrongTransactionStatusException" => $"Wrong transaction status: {ex.Message}",
      _ when typeName.Contains("Revit") => $"Revit error ({typeName.Split('.').Last()}): {ex.Message}",
      _ => $"Revit operation failed: {ex.Message}"
    };
  }
  public static async Task ProcessException<T>(
    string modelCardId,
    SpeckleRevitTaskException ex,
    ILogger<T> logger,
    IReceiveBindingUICommands commands
  )
    where T : IBinding
  {
    Exception e = ex.InnerException.NotNull();
    while (e is SpeckleRevitTaskException srte)
    {
      e = srte.InnerException.NotNull();
    }
    if (e is OperationCanceledException)
    {
      // SWALLOW -> UI handles it immediately, so we do not need to handle anything for now!
      // Idea for later -> when cancel called, create promise from UI to solve it later with this catch block.
      // So have 3 state on UI -> Cancellation clicked -> Cancelling -> Cancelled
      return;
    }
    //log everything though
    logger.LogModelCardHandledError(ex);
    //always process the inner exception
    await commands.SetModelError(modelCardId, e);
  }
}
