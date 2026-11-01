using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Storage;

namespace Harbor.Glass;

/// <summary>
/// Offscreen captures for checking layouts without a live screen. Only active when <c>HARBOR_UI_CAPTURE</c> names a
/// folder: creating <c>request.txt</c> there renders every open window's XAML over the glass's solid color to
/// <c>&lt;window title&gt;.png</c> and deletes the request. The glass material itself is not in these images.
/// </summary>
internal static class GlassCapture
{
    static readonly string? folder = Environment.GetEnvironmentVariable("HARBOR_UI_CAPTURE");
    static DispatcherTimer? timer;

    internal static void Start()
    {
        if (folder is null || timer is not null) return;
        Directory.CreateDirectory(folder);
        timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        timer.Tick += async (_, _) =>
        {
            var request = Path.Combine(folder, "request.txt");
            if (!File.Exists(request)) return;
            File.Delete(request);
            foreach (var window in GlassWindow.All.ToArray())
            {
                try { await Capture(window); }
                catch (Exception error) { await File.WriteAllTextAsync(Path.Combine(folder, "error.txt"), error.ToString()); }
            }
            await File.WriteAllTextAsync(Path.Combine(folder, "done.txt"), DateTime.Now.ToString("O"));
        };
        timer.Start();
    }

    static async Task Capture(GlassWindow window)
    {
        var bitmap = new RenderTargetBitmap();
        await bitmap.RenderAsync(window.Content);
        var pixels = (await bitmap.GetPixelsAsync()).ToArray();
        // Premultiplied BGRA over the solid glass color.
        bool dark = window.Content.ActualTheme == ElementTheme.Dark;
        byte back = dark ? (byte)22 : (byte)249;
        for (int i = 0; i < pixels.Length; i += 4)
        {
            int alpha = pixels[i + 3];
            for (int c = 0; c < 3; c++) pixels[i + c] = (byte)Math.Min(255, pixels[i + c] + back * (255 - alpha) / 255);
            pixels[i + 3] = 255;
        }
        var name = string.Concat(window.Title.Split(Path.GetInvalidFileNameChars())) + ".png";
        var file = await (await StorageFolder.GetFolderFromPathAsync(folder)).CreateFileAsync(name, CreationCollisionOption.ReplaceExisting);
        using var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        double dpi = 96 * (window.XamlRoot?.RasterizationScale ?? 1);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore, (uint)bitmap.PixelWidth, (uint)bitmap.PixelHeight, dpi, dpi, pixels);
        await encoder.FlushAsync();
    }
}
