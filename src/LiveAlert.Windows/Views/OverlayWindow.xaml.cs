using System.Globalization;
using System.Windows;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaColor = System.Windows.Media.Color;
using MediaColorConverter = System.Windows.Media.ColorConverter;
using MediaFontFamily = System.Windows.Media.FontFamily;
using MediaFormattedText = System.Windows.Media.FormattedText;
using MediaPixelFormats = System.Windows.Media.PixelFormats;
using SolidMediaBrush = System.Windows.Media.SolidColorBrush;
using TypefaceMedia = System.Windows.Media.Typeface;
using BitmapSourceMedia = System.Windows.Media.Imaging.BitmapSource;
using RenderTargetBitmapMedia = System.Windows.Media.Imaging.RenderTargetBitmap;
using DrawingVisualMedia = System.Windows.Media.DrawingVisual;
using DrawingContextMedia = System.Windows.Media.DrawingContext;
using LiveAlert.Core;
using LiveAlert.Windows.Services;
using Forms = System.Windows.Forms;

namespace LiveAlert.Windows.Views;

public partial class OverlayWindow : Window
{
    private const double EmergencyCycleSeconds = 1.0;
    private const double StripeCycleSeconds = 0.2;
    private const double CenterCycleSeconds = 4.0;
    private string _messageText = string.Empty;

    public static readonly DependencyProperty BackgroundColorProperty =
        DependencyProperty.Register(nameof(BackgroundColor), typeof(MediaBrush), typeof(OverlayWindow),
            new PropertyMetadata(MediaBrushes.Red));

    public static readonly DependencyProperty TextBrushProperty =
        DependencyProperty.Register(nameof(TextBrush), typeof(MediaBrush), typeof(OverlayWindow),
            new PropertyMetadata(MediaBrushes.Black));

    public OverlayWindow()
    {
        InitializeComponent();
        DataContext = this;
        Loaded += (_, _) => Dispatcher.BeginInvoke(RefreshVisuals, System.Windows.Threading.DispatcherPriority.Loaded);
        SizeChanged += (_, _) =>
        {
            if (IsLoaded)
            {
                Dispatcher.BeginInvoke(RefreshVisuals, System.Windows.Threading.DispatcherPriority.Loaded);
            }
        };
    }

    public event Action? BandClicked;

    public MediaBrush BackgroundColor
    {
        get => (MediaBrush)GetValue(BackgroundColorProperty);
        set => SetValue(BackgroundColorProperty, value);
    }

    public MediaBrush TextBrush
    {
        get => (MediaBrush)GetValue(TextBrushProperty);
        set => SetValue(TextBrushProperty, value);
    }

    public void Apply(AlertConfig alert, AlertOptions options)
    {
        var screen = Forms.Screen.PrimaryScreen!;
        var bounds = screen.Bounds;
        var height = Math.Clamp(options.BandHeightPx, 96, bounds.Height);

        Width = bounds.Width;
        Height = height;
        Left = bounds.Left;
        Top = options.BandPosition switch
        {
            "center" => bounds.Top + ((bounds.Height - height) / 2d),
            "bottom" => bounds.Bottom - height,
            _ => bounds.Top
        };

        BackgroundColor = CreateBrush(alert.Colors.Background, System.Windows.Media.Colors.Red);
        TextBrush = CreateBrush(alert.Colors.Text, System.Windows.Media.Colors.Black);

        var label = string.IsNullOrWhiteSpace(alert.Label) ? "(no label)" : alert.Label.Trim();
        _messageText = string.IsNullOrWhiteSpace(alert.Message)
            ? $"警告　{label} がライブ開始"
            : alert.Message.Replace("{label}", label, StringComparison.Ordinal);

        RefreshVisuals();
    }

    private void RefreshVisuals()
    {
        if (!IsLoaded || !CanRender())
        {
            return;
        }

        var background = GetSolidColor(BackgroundColor, System.Windows.Media.Colors.Red);
        var text = GetSolidColor(TextBrush, System.Windows.Media.Colors.Black);

        ConfigureTextLayer(
            TopEmergencyView,
            "EMERGENCY",
            background,
            text,
            new TypefaceMedia(new System.Windows.Media.FontFamily("Yu Gothic UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
            EmergencyCycleSeconds);

        ConfigureTextLayer(
            BottomEmergencyView,
            "EMERGENCY",
            background,
            text,
            new TypefaceMedia(new System.Windows.Media.FontFamily("Yu Gothic UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
            EmergencyCycleSeconds);

        ConfigureTextLayer(
            CenterMessageView,
            _messageText,
            background,
            text,
            CreateCenterTypeface(),
            CenterCycleSeconds);

        ConfigureStripeLayer(TopStripeView, background, text, StripeCycleSeconds);
        ConfigureStripeLayer(BottomStripeView, background, text, StripeCycleSeconds);
    }

    private void ConfigureTextLayer(
        ScrollingBitmapView view,
        string text,
        MediaColor background,
        MediaColor textColor,
        TypefaceMedia typeface,
        double cycleSeconds)
    {
        var viewportWidth = Math.Max(1, (int)Math.Ceiling(view.ActualWidth));
        var height = Math.Max(1, (int)Math.Ceiling(view.ActualHeight));
        var (bitmap, unitWidth) = CreateTextLayerBitmap(text, viewportWidth, height, background, textColor, typeface);
        view.Configure(bitmap, unitWidth, cycleSeconds);
    }

    private void ConfigureStripeLayer(
        ScrollingBitmapView view,
        MediaColor background,
        MediaColor stripeColor,
        double cycleSeconds)
    {
        var height = Math.Max(1, (int)Math.Ceiling(view.ActualHeight));
        var (bitmap, unitWidth) = CreateStripeTileBitmap(height, background, stripeColor);
        view.Configure(bitmap, unitWidth, cycleSeconds);
    }

    private static (BitmapSourceMedia Bitmap, int UnitWidth) CreateTextLayerBitmap(
        string text,
        int displayWidth,
        int height,
        MediaColor background,
        MediaColor textColor,
        TypefaceMedia typeface)
    {
        var padding = Math.Max(6, height / 6);
        var fontSize = Math.Max(12, height - padding * 2);
        var unitText = text.Trim() + "   ";
        var formatted = CreateFormattedText(unitText, typeface, fontSize, textColor, 1.0);
        var unitWidth = Math.Max(1, (int)Math.Ceiling(formatted.WidthIncludingTrailingWhitespace + padding * 2));
        var internalWidth = CalculateInternalWidth(displayWidth, unitWidth);

        var visual = new DrawingVisualMedia();
        using (DrawingContextMedia dc = visual.RenderOpen())
        {
            dc.DrawRectangle(CreateBrush(background), null, new Rect(0, 0, internalWidth, height));
            var baselineY = Math.Max(0d, (height - formatted.Height) / 2d);
            for (var x = 0; x < internalWidth; x += unitWidth)
            {
                dc.DrawText(formatted, new System.Windows.Point(x + padding, baselineY));
            }
        }

        var bitmap = new RenderTargetBitmapMedia(internalWidth, height, 96, 96, MediaPixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return (bitmap, unitWidth);
    }

    private static MediaFormattedText CreateFormattedText(
        string text,
        TypefaceMedia typeface,
        double fontSize,
        MediaColor textColor,
        double pixelsPerDip)
    {
        return new MediaFormattedText(
            text,
            CultureInfo.InvariantCulture,
            System.Windows.FlowDirection.LeftToRight,
            typeface,
            fontSize,
            CreateBrush(textColor),
            pixelsPerDip);
    }

    private static (BitmapSourceMedia Bitmap, int UnitWidth) CreateStripeTileBitmap(int height, MediaColor backgroundColor, MediaColor stripeColor)
    {
        var stripeWidth = Math.Max(6, height / 3);
        var unitWidth = Math.Max(1, stripeWidth * 2);
        var stride = unitWidth * 4;
        var pixels = new byte[stride * height];

        for (var y = 0; y < height; y++)
        {
            var rowOffset = y * stride;
            for (var x = 0; x < unitWidth; x++)
            {
                var useStripe = ((x + y) % unitWidth) < stripeWidth;
                var color = useStripe ? stripeColor : backgroundColor;
                var pixelOffset = rowOffset + (x * 4);
                pixels[pixelOffset + 0] = color.B;
                pixels[pixelOffset + 1] = color.G;
                pixels[pixelOffset + 2] = color.R;
                pixels[pixelOffset + 3] = color.A;
            }
        }

        var bitmap = BitmapSourceMedia.Create(unitWidth, height, 96, 96, MediaPixelFormats.Bgra32, null, pixels, stride);
        bitmap.Freeze();
        return (bitmap, unitWidth);
    }

    private static int CalculateInternalWidth(int screenWidthPx, int unitWidthPx)
    {
        var safeWidth = Math.Max(screenWidthPx + unitWidthPx, screenWidthPx + 1);
        var repeats = (int)Math.Ceiling(safeWidth / (double)unitWidthPx);
        return Math.Max(unitWidthPx, repeats * unitWidthPx);
    }

    private static TypefaceMedia CreateCenterTypeface()
    {
        try
        {
            var family = new MediaFontFamily(AppAssets.CenterFontBaseUri, AppAssets.CenterFontFamilyPath);
            return new TypefaceMedia(family, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        }
        catch
        {
        }

        return new TypefaceMedia(new System.Windows.Media.FontFamily("Yu Mincho"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
    }

    private static MediaBrush CreateBrush(string? hex, MediaColor fallback)
    {
        return new SolidMediaBrush(GetColor(hex, fallback));
    }

    private static SolidMediaBrush CreateBrush(MediaColor color)
    {
        var brush = new SolidMediaBrush(color);
        brush.Freeze();
        return brush;
    }

    private static MediaColor GetSolidColor(MediaBrush brush, MediaColor fallback)
    {
        return brush is SolidMediaBrush solid ? solid.Color : fallback;
    }

    private static MediaColor GetColor(string? hex, MediaColor fallback)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return fallback;
        }

        var value = hex.Trim();
        if (!value.StartsWith('#'))
        {
            value = $"#{value}";
        }

        if (MediaColorConverter.ConvertFromString(value) is MediaColor color)
        {
            return color;
        }

        return fallback;
    }

    private void HandleBandClicked(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        BandClicked?.Invoke();
    }

    private bool CanRender()
    {
        return TopEmergencyView.ActualWidth > 0 &&
               TopEmergencyView.ActualHeight > 0 &&
               TopStripeView.ActualWidth > 0 &&
               TopStripeView.ActualHeight > 0 &&
               CenterMessageView.ActualWidth > 0 &&
               CenterMessageView.ActualHeight > 0 &&
               BottomStripeView.ActualWidth > 0 &&
               BottomStripeView.ActualHeight > 0 &&
               BottomEmergencyView.ActualWidth > 0 &&
               BottomEmergencyView.ActualHeight > 0;
    }
}
