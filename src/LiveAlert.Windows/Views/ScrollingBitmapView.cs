using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace LiveAlert.Windows.Views;

public sealed class ScrollingBitmapView : FrameworkElement
{
    public static readonly DependencyProperty OffsetProperty =
        DependencyProperty.Register(
            nameof(Offset),
            typeof(double),
            typeof(ScrollingBitmapView),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    private BitmapSource? _bitmap;
    private double _unitWidth;

    public double Offset
    {
        get => (double)GetValue(OffsetProperty);
        set => SetValue(OffsetProperty, value);
    }

    public void Configure(BitmapSource bitmap, int unitWidth, double cycleSeconds)
    {
        _bitmap = bitmap;
        _unitWidth = Math.Max(1, unitWidth);
        BeginAnimation(OffsetProperty, null);
        Offset = 0d;

        var animation = new DoubleAnimation(0d, _unitWidth, TimeSpan.FromSeconds(cycleSeconds))
        {
            RepeatBehavior = RepeatBehavior.Forever
        };
        BeginAnimation(OffsetProperty, animation, HandoffBehavior.SnapshotAndReplace);
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        if (_bitmap is null)
        {
            return;
        }

        var bitmapWidth = _bitmap.Width;
        var bitmapHeight = Math.Max(1d, ActualHeight);
        for (double x = -Offset; x < ActualWidth; x += bitmapWidth)
        {
            drawingContext.DrawImage(_bitmap, new Rect(x, 0, bitmapWidth, bitmapHeight));
        }
    }
}
