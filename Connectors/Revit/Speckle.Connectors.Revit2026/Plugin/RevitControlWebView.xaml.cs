using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Core;
using Speckle.Connectors.DUI.Bindings;
using Speckle.Connectors.DUI.Bridge;
using Speckle.Connectors.Revit.Plugin;

namespace Speckle.Connectors.Revit2026.Plugin;

public sealed partial class RevitControlWebView : UserControl, IBrowserScriptExecutor, IDisposable
{
  private readonly IServiceProvider _serviceProvider;
  private readonly IRevitTask _revitTask;

  public RevitControlWebView(IServiceProvider serviceProvider, IRevitTask revitTask)
  {
    _serviceProvider = serviceProvider;
    _revitTask = revitTask;
    InitializeComponent();

    Browser.CoreWebView2InitializationCompleted += (sender, args) =>
      _serviceProvider
        .GetRequiredService<ITopLevelExceptionHandler>()
        .CatchUnhandled(() => OnInitialized(sender, args));

    // Add navigation event handlers for debugging
    Browser.NavigationStarting += (sender, args) =>
    {
      Console.WriteLine($"WebView2 Navigation Starting: {args.Uri}");
    };

    Browser.NavigationCompleted += (sender, args) =>
    {
      Console.WriteLine($"WebView2 Navigation Completed: Success={args.IsSuccess}");
      if (!args.IsSuccess)
      {
        Console.WriteLine($"WebView2 Navigation Failed: {args.WebErrorStatus}");
      }
    };

    // DOMContentLoaded event - remove this as it's not available in this WebView2 version
    // Browser.DOMContentLoaded += (sender, args) =>
    // {
    //   Console.WriteLine("WebView2 DOM Content Loaded");
    // };
  }

  public bool IsBrowserInitialized => Browser.IsInitialized;

  public object BrowserElement => Browser;

  public void ExecuteScript(string script)
  {
    if (!Browser.IsInitialized)
    {
      throw new InvalidOperationException("Failed to execute script, Webview2 is not initialized yet.");
    }
    _revitTask.Run(() => Browser.ExecuteScriptAsync(script));
  }

  public void SendProgress(string script)
  {
    if (!Browser.IsInitialized)
    {
      throw new InvalidOperationException("Failed to execute script, Webview2 is not initialized yet.");
    }
    //always invoke even on the main thread because it's better somehow
    Browser.Dispatcher.Invoke(
      //fire and forget
      () => Browser.ExecuteScriptAsync(script),
      DispatcherPriority.Background
    );
  }

  private void OnInitialized(object? sender, CoreWebView2InitializationCompletedEventArgs e)
  {
    Console.WriteLine($"WebView2 Browser Version: {CoreWebView2Environment.GetAvailableBrowserVersionString()}");
    Console.WriteLine($"WebView2 Initialization Success: {e.IsSuccess}");

    if (!e.IsSuccess)
    {
      Console.WriteLine($"WebView2 Initialization Exception: {e.InitializationException}");
      throw new InvalidOperationException("Webview Failed to initialize", e.InitializationException);
    }

    Console.WriteLine($"WebView2 Source URL: {Browser.Source}");
    Console.WriteLine("WebView2 Successfully initialized, setting up bindings...");

    // Configure WebView2 settings for localhost access
    Browser.CoreWebView2.Settings.IsGeneralAutofillEnabled = false;
    Browser.CoreWebView2.Settings.IsPasswordAutosaveEnabled = false;
    Browser.CoreWebView2.Settings.AreDevToolsEnabled = true;
    Browser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
    Browser.CoreWebView2.Settings.AreHostObjectsAllowed = true;
    Browser.CoreWebView2.Settings.IsWebMessageEnabled = true;

    // Add localhost to allowed origins
    Browser.CoreWebView2.AddWebResourceRequestedFilter("*", Microsoft.Web.WebView2.Core.CoreWebView2WebResourceContext.All);

    Console.WriteLine("WebView2 Settings configured");

    // We use Lazy here to delay creating the binding until after the Browser is fully initialized.
    // Otherwise the Browser cannot respond to any requests to ExecuteScriptAsyncMethod
    foreach (var binding in _serviceProvider.GetRequiredService<IEnumerable<IBinding>>())
    {
      SetupBinding(binding);
    }

    Console.WriteLine("WebView2 Bindings setup completed");

    // Force navigation to the URL in case XAML binding didn't work
    var targetUrl = "http://localhost:3000/";
    var testUrl = "https://www.google.com"; // Test with external URL first
    Console.WriteLine($"Target URL: {targetUrl}");

    // First try a simple HTML test to see if WebView2 works at all
    var testHtml = @"
    <!DOCTYPE html>
    <html>
    <head><title>WebView2 Test</title></head>
    <body style='background: red; color: white; font-size: 24px; padding: 20px;'>
        <h1>WebView2 is working!</h1>
        <p>If you see this, WebView2 can load content.</p>
        <button onclick='window.location.href=""" + targetUrl + @"""'>Load Speckle UI</button>
    </body>
    </html>";

    Console.WriteLine("Loading test HTML first...");
    Browser.CoreWebView2.NavigateToString(testHtml);

    // After 3 seconds, test with Google first, then localhost
    Task.Delay(3000).ContinueWith(_ =>
    {
      Browser.Dispatcher.Invoke(() =>
      {
        Console.WriteLine($"Testing with external URL: {testUrl}");
        Browser.CoreWebView2.Navigate(testUrl);

        // After another 5 seconds, try localhost
        Task.Delay(5000).ContinueWith(__ =>
        {
          Browser.Dispatcher.Invoke(() =>
          {
            Console.WriteLine($"Now navigating to localhost: {targetUrl}");
            Browser.CoreWebView2.Navigate(targetUrl);
          });
        }, TaskScheduler.Default);
      });
    }, TaskScheduler.Default);
  }

  /// <remark>
  /// This must be called on the Main thread
  /// </remark>
  private void SetupBinding(IBinding binding)
  {
    binding.Parent.AssociateWithBinding(binding);
    Browser.CoreWebView2.AddHostObjectToScript(binding.Name, binding.Parent);
  }

  public void ShowDevTools() => Browser.CoreWebView2.OpenDevToolsWindow();

  //https://github.com/MicrosoftEdge/WebView2Feedback/issues/2161
  public void Dispose() => Browser.Dispatcher.Invoke(() => Browser.Dispose(), DispatcherPriority.Send);
}
