using Android.App;
using Android.Content;
using Android.Content.PM;
using Microsoft.Maui.ApplicationModel;

namespace LiveAlert;

[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    private const int SaveDocumentRequestCode = 4097;
    private const int OpenDocumentRequestCode = 4098;
    private static TaskCompletionSource<Android.Net.Uri?>? _saveDocumentTcs;
    private static TaskCompletionSource<Android.Net.Uri?>? _openDocumentTcs;

    internal static Task<Android.Net.Uri?> CreateDocumentAsync(string fileName, string mimeType)
    {
        var activity = Platform.CurrentActivity;
        if (activity == null)
        {
            return Task.FromResult<Android.Net.Uri?>(null);
        }

        var tcs = new TaskCompletionSource<Android.Net.Uri?>();
        _saveDocumentTcs = tcs;
        var intent = new Intent(Intent.ActionCreateDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType(mimeType);
        intent.PutExtra(Intent.ExtraTitle, fileName);
        activity.StartActivityForResult(intent, SaveDocumentRequestCode);
        return tcs.Task;
    }

    internal static Task<Android.Net.Uri?> OpenDocumentAsync(string[] mimeTypes)
    {
        var activity = Platform.CurrentActivity;
        if (activity == null)
        {
            return Task.FromResult<Android.Net.Uri?>(null);
        }

        var tcs = new TaskCompletionSource<Android.Net.Uri?>();
        _openDocumentTcs = tcs;
        var intent = new Intent(Intent.ActionOpenDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType("*/*");
        intent.PutExtra(Intent.ExtraMimeTypes, mimeTypes);
        activity.StartActivityForResult(intent, OpenDocumentRequestCode);
        return tcs.Task;
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        if (requestCode == SaveDocumentRequestCode)
        {
            var tcs = _saveDocumentTcs;
            _saveDocumentTcs = null;
            if (tcs == null)
            {
                return;
            }

            if (resultCode == Result.Ok)
            {
                tcs.TrySetResult(data?.Data);
            }
            else
            {
                tcs.TrySetResult(null);
            }
            return;
        }

        if (requestCode == OpenDocumentRequestCode)
        {
            var tcs = _openDocumentTcs;
            _openDocumentTcs = null;
            if (tcs == null)
            {
                return;
            }

            if (resultCode == Result.Ok)
            {
                tcs.TrySetResult(data?.Data);
            }
            else
            {
                tcs.TrySetResult(null);
            }
        }
    }
}
