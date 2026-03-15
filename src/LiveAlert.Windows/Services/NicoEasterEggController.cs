using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LiveAlert.Core;
using Forms = System.Windows.Forms;

namespace LiveAlert.Windows.Services;

internal sealed class NicoEasterEggController : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly Random _random = new();
    private readonly List<ActiveSprite> _sprites = new();
    private readonly List<PendingWindowMove> _pendingMoves = new();
    private NicoImageResources? _resources;
    private TimeSpan? _startRenderingTime;
    private TimeSpan? _lastRenderingTime;
    private TimeSpan _nextSpawnAt;
    private Forms.Screen? _screen;
    private bool _renderingAttached;
    private bool _disposed;

    public NicoEasterEggController()
        : this(System.Windows.Application.Current.Dispatcher)
    {
    }

    internal NicoEasterEggController(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public event Action? SpriteClicked;

    public void Start(AlertConfig alert)
    {
        VerifyAccess();

        Stop();
        if (!NicoEasterEggPhysics.ShouldActivate(alert.Label))
        {
            return;
        }

        try
        {
            _resources ??= NicoImageResources.Load();
            _screen = Forms.Screen.PrimaryScreen;
            if (_screen is null)
            {
                return;
            }

            _startRenderingTime = null;
            _lastRenderingTime = null;
            _nextSpawnAt = TimeSpan.FromSeconds(NicoEasterEggPhysics.NextSpawnDelaySeconds(_random));
            AttachRendering();
        }
        catch (Exception ex)
        {
            AppLog.Warn($"Failed to start nico easter egg: {ex.Message}");
            Stop();
        }
    }

    public void Stop()
    {
        VerifyAccess();

        DetachRendering();
        _startRenderingTime = null;
        _lastRenderingTime = null;
        _nextSpawnAt = default;
        _screen = null;
        _pendingMoves.Clear();
        ClearSprites();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Stop();
        _resources?.Dispose();
        _resources = null;
        _disposed = true;
    }

    private void HandleRendering(object? sender, EventArgs e)
    {
        if (_screen is null || _resources is null)
        {
            return;
        }

        try
        {
            if (e is not RenderingEventArgs renderingArgs)
            {
                return;
            }

            var now = renderingArgs.RenderingTime;
            _startRenderingTime ??= now;
            var relativeNow = now - _startRenderingTime.Value;
            var deltaSeconds = _lastRenderingTime is null
                ? 0d
                : Math.Max(0d, (now - _lastRenderingTime.Value).TotalSeconds);
            _lastRenderingTime = now;

            if (deltaSeconds > 0d)
            {
                UpdateSprites(deltaSeconds, _screen.Bounds, _resources);
            }

            TrySpawn(relativeNow, _screen.Bounds, _resources);
        }
        catch (Exception ex)
        {
            AppLog.Warn($"Failed to update nico easter egg: {ex.Message}");
            Stop();
        }
    }

    private void TrySpawn(TimeSpan now, System.Drawing.Rectangle bounds, NicoImageResources resources)
    {
        if (_sprites.Count >= NicoEasterEggTuning.MaxSprites || now < _nextSpawnAt)
        {
            return;
        }

        var motion = NicoEasterEggPhysics.CreateInitialMotion(_random, bounds.Width, resources.Width);
        var mirrored = NicoEasterEggPhysics.ShouldUseMirroredBitmap(motion.HorizontalVelocity);
        var placement = NicoEasterEggPhysics.GetPlacement(motion, resources.Width, resources.Height);
        var window = new NicoSpriteWindow(
            mirrored ? resources.MirroredBitmap : resources.NormalBitmap,
            resources.Width,
            resources.Height,
            bounds.Left + placement.Left,
            bounds.Top + placement.Top);
        window.Clicked += HandleSpriteClicked;
        _sprites.Add(new ActiveSprite(window, motion, mirrored));

        _nextSpawnAt = now + TimeSpan.FromSeconds(NicoEasterEggPhysics.NextSpawnDelaySeconds(_random));
    }

    private void UpdateSprites(double deltaSeconds, System.Drawing.Rectangle bounds, NicoImageResources resources)
    {
        _pendingMoves.Clear();
        for (var index = _sprites.Count - 1; index >= 0; index--)
        {
            var sprite = _sprites[index];
            var bounce = _random.Next(NicoEasterEggTuning.BounceChancePerFrameDenominator) == 0;
            var bouncedHorizontalVelocity = NicoEasterEggPhysics.RandomHorizontalVelocity(_random, resources.Width);
            var motion = NicoEasterEggPhysics.Step(
                sprite.Motion,
                deltaSeconds,
                resources.GravityPerSecondSquared,
                resources.Width,
                resources.Height,
                bounce,
                bouncedHorizontalVelocity);

            if (NicoEasterEggPhysics.ShouldDespawn(motion.Y, bounds.Height, resources.Height))
            {
                RemoveSprite(index);
                continue;
            }

            var mirrored = NicoEasterEggPhysics.ShouldUseMirroredBitmap(motion.HorizontalVelocity);
            if (mirrored != sprite.Mirrored)
            {
                sprite.Window.SetBitmap(mirrored ? resources.MirroredBitmap : resources.NormalBitmap);
            }

            var placement = NicoEasterEggPhysics.GetPlacement(motion, resources.Width, resources.Height);
            _pendingMoves.Add(new PendingWindowMove(
                sprite.Window,
                bounds.Left + placement.Left,
                bounds.Top + placement.Top));
            _sprites[index] = sprite with
            {
                Motion = motion,
                Mirrored = mirrored
            };
        }

        ApplyDeferredMoves();
    }

    private void HandleSpriteClicked()
    {
        SpriteClicked?.Invoke();
    }

    private void RemoveSprite(int index)
    {
        var sprite = _sprites[index];
        sprite.Window.Clicked -= HandleSpriteClicked;
        sprite.Window.Dispose();
        _sprites.RemoveAt(index);
    }

    private void ClearSprites()
    {
        for (var index = _sprites.Count - 1; index >= 0; index--)
        {
            RemoveSprite(index);
        }
    }

    private void VerifyAccess()
    {
        if (!_dispatcher.CheckAccess())
        {
            throw new InvalidOperationException("NicoEasterEggController must be used on the UI dispatcher.");
        }
    }

    private void AttachRendering()
    {
        if (_renderingAttached)
        {
            return;
        }

        CompositionTarget.Rendering += HandleRendering;
        _renderingAttached = true;
    }

    private void DetachRendering()
    {
        if (!_renderingAttached)
        {
            return;
        }

        CompositionTarget.Rendering -= HandleRendering;
        _renderingAttached = false;
    }

    private void ApplyDeferredMoves()
    {
        if (_pendingMoves.Count == 0)
        {
            return;
        }

        var deferredHandle = NativeMethods.BeginDeferWindowPos(_pendingMoves.Count);
        if (deferredHandle == nint.Zero)
        {
            throw new InvalidOperationException($"BeginDeferWindowPos failed: {Marshal.GetLastWin32Error()}");
        }

        foreach (var pendingMove in _pendingMoves)
        {
            pendingMove.Window.UpdateCachedPosition(pendingMove.Left, pendingMove.Top);
            deferredHandle = NativeMethods.DeferWindowPos(
                deferredHandle,
                pendingMove.Window.Handle,
                nint.Zero,
                pendingMove.Left,
                pendingMove.Top,
                0,
                0,
                NativeMethods.SWP_NOACTIVATE |
                NativeMethods.SWP_NOSIZE |
                NativeMethods.SWP_NOZORDER |
                NativeMethods.SWP_SHOWWINDOW);
            if (deferredHandle == nint.Zero)
            {
                throw new InvalidOperationException($"DeferWindowPos failed: {Marshal.GetLastWin32Error()}");
            }
        }

        if (!NativeMethods.EndDeferWindowPos(deferredHandle))
        {
            throw new InvalidOperationException($"EndDeferWindowPos failed: {Marshal.GetLastWin32Error()}");
        }
    }

    private readonly record struct ActiveSprite(NicoSpriteWindow Window, NicoSpriteMotion Motion, bool Mirrored);

    private readonly record struct PendingWindowMove(NicoSpriteWindow Window, int Left, int Top);

    private sealed class NicoImageResources : IDisposable
    {
        public NicoImageResources(nint normalBitmap, nint mirroredBitmap, int width, int height)
        {
            NormalBitmap = normalBitmap;
            MirroredBitmap = mirroredBitmap;
            Width = width;
            Height = height;
            GravityPerSecondSquared = Math.Max(1, height) * NicoEasterEggTuning.GravityMultiplier;
        }

        public nint NormalBitmap { get; }

        public nint MirroredBitmap { get; }

        public int Width { get; }

        public int Height { get; }

        public double GravityPerSecondSquared { get; }

        public static NicoImageResources Load()
        {
            var normalSource = LoadBitmapSource(AppAssets.NicoImageUri);
            var mirroredSource = CreateMirroredBitmapSource(normalSource);
            var normalBitmap = CreateBitmapHandle(normalSource);
            try
            {
                var mirroredBitmap = CreateBitmapHandle(mirroredSource);
                return new NicoImageResources(normalBitmap, mirroredBitmap, normalSource.PixelWidth, normalSource.PixelHeight);
            }
            catch
            {
                NativeMethods.DeleteObject(normalBitmap);
                throw;
            }
        }

        public void Dispose()
        {
            NativeMethods.DeleteObject(NormalBitmap);
            NativeMethods.DeleteObject(MirroredBitmap);
        }

        private static BitmapSource LoadBitmapSource(Uri resourceUri)
        {
            using var stream = AppAssets.OpenResourceStream(resourceUri);
            var frame = BitmapFrame.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var converted = new FormatConvertedBitmap(frame, PixelFormats.Pbgra32, null, 0d);
            converted.Freeze();
            return converted;
        }

        private static BitmapSource CreateMirroredBitmapSource(BitmapSource source)
        {
            var mirrored = new TransformedBitmap(source, new ScaleTransform(-1d, 1d, source.PixelWidth / 2d, source.PixelHeight / 2d));
            mirrored.Freeze();
            return mirrored;
        }

        private static nint CreateBitmapHandle(BitmapSource source)
        {
            var stride = source.PixelWidth * 4;
            var pixels = new byte[stride * source.PixelHeight];
            source.CopyPixels(pixels, stride, 0);

            var info = new NativeMethods.BITMAPINFO
            {
                bmiHeader = new NativeMethods.BITMAPINFOHEADER
                {
                    biSize = (uint)Marshal.SizeOf<NativeMethods.BITMAPINFOHEADER>(),
                    biWidth = source.PixelWidth,
                    biHeight = -source.PixelHeight,
                    biPlanes = 1,
                    biBitCount = 32,
                    biCompression = NativeMethods.BI_RGB
                },
                bmiColors = [0]
            };

            var screenDc = NativeMethods.GetDC(nint.Zero);
            if (screenDc == nint.Zero)
            {
                throw new InvalidOperationException("GetDC failed.");
            }

            try
            {
                var bitmapHandle = NativeMethods.CreateDIBSection(screenDc, ref info, NativeMethods.DIB_RGB_COLORS, out var bits, nint.Zero, 0);
                if (bitmapHandle == nint.Zero || bits == nint.Zero)
                {
                    throw new InvalidOperationException("CreateDIBSection failed.");
                }

                Marshal.Copy(pixels, 0, bits, pixels.Length);
                return bitmapHandle;
            }
            finally
            {
                NativeMethods.ReleaseDC(nint.Zero, screenDc);
            }
        }
    }

    private sealed class NicoSpriteWindow : NativeWindow, IDisposable
    {
        private readonly int _width;
        private readonly int _height;
        private int _left;
        private int _top;
        private nint _bitmapHandle;
        private bool _disposed;

        public NicoSpriteWindow(nint bitmapHandle, int width, int height, int left, int top)
        {
            _bitmapHandle = bitmapHandle;
            _width = width;
            _height = height;
            _left = left;
            _top = top;

            var createParams = new CreateParams
            {
                Caption = string.Empty,
                X = left,
                Y = top,
                Width = width,
                Height = height,
                Style = NativeMethods.WS_POPUP,
                ExStyle = NativeMethods.WS_EX_LAYERED |
                          NativeMethods.WS_EX_TOOLWINDOW |
                          NativeMethods.WS_EX_TOPMOST |
                          NativeMethods.WS_EX_NOACTIVATE
            };

            CreateHandle(createParams);
            ApplyLayeredBitmap();
            NativeMethods.ShowWindow(Handle, NativeMethods.SW_SHOWNOACTIVATE);
        }

        public event Action? Clicked;

        public void SetBitmap(nint bitmapHandle)
        {
            if (_disposed || _bitmapHandle == bitmapHandle)
            {
                return;
            }

            _bitmapHandle = bitmapHandle;
            ApplyLayeredBitmap();
        }

        public void UpdateCachedPosition(int left, int top)
        {
            if (_disposed)
            {
                return;
            }

            _left = left;
            _top = top;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (Handle != nint.Zero)
            {
                DestroyHandle();
            }
        }

        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case NativeMethods.WM_MOUSEACTIVATE:
                    m.Result = (nint)NativeMethods.MA_NOACTIVATE;
                    return;
                case NativeMethods.WM_NCHITTEST:
                    m.Result = (nint)NativeMethods.HTCLIENT;
                    return;
                case NativeMethods.WM_LBUTTONUP:
                    Clicked?.Invoke();
                    m.Result = nint.Zero;
                    return;
            }

            base.WndProc(ref m);
        }

        private void ApplyLayeredBitmap()
        {
            var screenDc = NativeMethods.GetDC(nint.Zero);
            if (screenDc == nint.Zero)
            {
                throw new InvalidOperationException("GetDC failed.");
            }

            var memoryDc = NativeMethods.CreateCompatibleDC(screenDc);
            if (memoryDc == nint.Zero)
            {
                NativeMethods.ReleaseDC(nint.Zero, screenDc);
                throw new InvalidOperationException("CreateCompatibleDC failed.");
            }

            var oldBitmap = NativeMethods.SelectObject(memoryDc, _bitmapHandle);

            try
            {
                var destination = new NativeMethods.POINT(_left, _top);
                var size = new NativeMethods.SIZE(_width, _height);
                var source = new NativeMethods.POINT(0, 0);
                var blend = new NativeMethods.BLENDFUNCTION
                {
                    BlendOp = NativeMethods.AC_SRC_OVER,
                    SourceConstantAlpha = 255,
                    AlphaFormat = NativeMethods.AC_SRC_ALPHA
                };

                var updated = NativeMethods.UpdateLayeredWindow(
                    Handle,
                    screenDc,
                    ref destination,
                    ref size,
                    memoryDc,
                    ref source,
                    0,
                    ref blend,
                    NativeMethods.ULW_ALPHA);
                if (!updated)
                {
                    throw new InvalidOperationException($"UpdateLayeredWindow failed: {Marshal.GetLastWin32Error()}");
                }
            }
            finally
            {
                NativeMethods.SelectObject(memoryDc, oldBitmap);
                NativeMethods.DeleteDC(memoryDc);
                NativeMethods.ReleaseDC(nint.Zero, screenDc);
            }
        }
    }

    private static class NativeMethods
    {
        public const int BI_RGB = 0;
        public const uint DIB_RGB_COLORS = 0;
        public const int HTCLIENT = 1;
        public const int MA_NOACTIVATE = 3;
        public const int SW_SHOWNOACTIVATE = 4;
        public const int SWP_NOSIZE = 0x0001;
        public const int SWP_NOZORDER = 0x0004;
        public const int SWP_NOACTIVATE = 0x0010;
        public const int SWP_SHOWWINDOW = 0x0040;
        public const int ULW_ALPHA = 0x00000002;
        public const byte AC_SRC_OVER = 0x00;
        public const byte AC_SRC_ALPHA = 0x01;
        public const int WM_MOUSEACTIVATE = 0x0021;
        public const int WM_NCHITTEST = 0x0084;
        public const int WM_LBUTTONUP = 0x0202;
        public const int WS_POPUP = unchecked((int)0x80000000);
        public const int WS_EX_LAYERED = 0x00080000;
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int WS_EX_TOPMOST = 0x00000008;
        public const int WS_EX_NOACTIVATE = 0x08000000;
        public static readonly nint HWND_TOPMOST = new(-1);

        [DllImport("gdi32.dll", SetLastError = true)]
        public static extern nint CreateCompatibleDC(nint hdc);

        [DllImport("gdi32.dll", SetLastError = true)]
        public static extern bool DeleteDC(nint hdc);

        [DllImport("gdi32.dll", SetLastError = true)]
        public static extern nint SelectObject(nint hdc, nint hgdiobj);

        [DllImport("gdi32.dll", SetLastError = true)]
        public static extern bool DeleteObject(nint hObject);

        [DllImport("gdi32.dll", SetLastError = true)]
        public static extern nint CreateDIBSection(
            nint hdc,
            [In] ref BITMAPINFO pbmi,
            uint usage,
            out nint ppvBits,
            nint hSection,
            uint offset);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern nint GetDC(nint hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int ReleaseDC(nint hWnd, nint hDc);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UpdateLayeredWindow(
            nint hWnd,
            nint hdcDst,
            ref POINT pptDst,
            ref SIZE psize,
            nint hdcSrc,
            ref POINT pptSrc,
            int crKey,
            ref BLENDFUNCTION pblend,
            int dwFlags);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern nint BeginDeferWindowPos(int nNumWindows);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern nint DeferWindowPos(
            nint hWinPosInfo,
            nint hWnd,
            nint hWndInsertAfter,
            int x,
            int y,
            int cx,
            int cy,
            int flags);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool EndDeferWindowPos(nint hWinPosInfo);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool ShowWindow(nint hWnd, int nCmdShow);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public POINT(int x, int y)
            {
                X = x;
                Y = y;
            }

            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SIZE
        {
            public SIZE(int cx, int cy)
            {
                CX = cx;
                CY = cy;
            }

            public int CX;
            public int CY;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct BLENDFUNCTION
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFO
        {
            public BITMAPINFOHEADER bmiHeader;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
            public uint[] bmiColors;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFOHEADER
        {
            public uint biSize;
            public int biWidth;
            public int biHeight;
            public short biPlanes;
            public short biBitCount;
            public int biCompression;
            public int biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public uint biClrUsed;
            public uint biClrImportant;
        }
    }
}
