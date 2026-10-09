using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

namespace Meow.LiquidGlass.Avalonia;

public class MeowLiquidGlassSurface : ContentControl
{
    // ---------- Styled Properties ----------

    public static readonly StyledProperty<float> BlurRadiusProperty =
        AvaloniaProperty.Register<MeowLiquidGlassSurface, float>(nameof(BlurRadius), 3f);

    public static readonly StyledProperty<float> BlurredEdgeProperty =
        AvaloniaProperty.Register<MeowLiquidGlassSurface, float>(nameof(BlurredEdgePercent), 0.15f);

    /// <summary>
    /// 特征速度（像素/秒）。速度等于它时，缩放系数约为 (Min+Max)/2。
    /// </summary>
    public static readonly StyledProperty<double> CharacteristicSpeedProperty =
        AvaloniaProperty.Register<MeowLiquidGlassSurface, double>(nameof(CharacteristicSpeed), 400.0);

    public static readonly StyledProperty<double> MinScaleProperty =
        AvaloniaProperty.Register<MeowLiquidGlassSurface, double>(nameof(MinScale), 0.7);

    public static readonly StyledProperty<double> MaxScaleProperty =
        AvaloniaProperty.Register<MeowLiquidGlassSurface, double>(nameof(MaxScale), 1.0);

    /// <summary>
    /// 启用运动缩放。默认为true
    /// </summary>
    public static readonly StyledProperty<bool> EnableMotionScaleProperty = AvaloniaProperty.Register<MeowLiquidGlassSurface, bool>(
        nameof(EnableMotionScale), true);

    public bool EnableMotionScale
    {
        get => GetValue(EnableMotionScaleProperty);
        set => SetValue(EnableMotionScaleProperty, value);
    }

    public float BlurRadius
    {
        get => GetValue(BlurRadiusProperty);
        set => SetValue(BlurRadiusProperty, value);
    }

    public float BlurredEdgePercent
    {
        get => GetValue(BlurredEdgeProperty);
        set => SetValue(BlurredEdgeProperty, value);
    }

    public double CharacteristicSpeed
    {
        get => GetValue(CharacteristicSpeedProperty);
        set => SetValue(CharacteristicSpeedProperty, value);
    }

    public double MinScale
    {
        get => GetValue(MinScaleProperty);
        set => SetValue(MinScaleProperty, value);
    }

    public double MaxScale
    {
        get => GetValue(MaxScaleProperty);
        set => SetValue(MaxScaleProperty, value);
    }

    // ---------- 内部状态 ----------

    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    // 用 TopLevel 坐标系的上一帧位置（不随控件尺寸变化）
    private Point _lastPosition;
    private double _lastTimeSec;

    private bool _monitorPointerMove;

    // 平滑后的速度（EMA）
    private double _smoothedVelocityX;
    private double _smoothedVelocityY;
    private const double VelocityEmaAlpha = 0.25; // 0~1，越小越平滑但越迟钝

    // 基准尺寸（用户设置的 Width/Height 或首次布局尺寸）
    private double _baseWidth;
    private double _baseHeight;
    private bool _baseCaptured;

    // 当前 / 目标缩放（用定时器平滑逼近）
    private double _currentScaleX = 1.0;
    private double _currentScaleY = 1.0;
    private double _targetScaleX = 1.0;
    private double _targetScaleY = 1.0;
    private const double ScaleEmaAlpha = 0.3; // 缩放靠近速度，越小越平滑

    private readonly DispatcherTimer _timer;

    // ---------- 构造 ----------

    public MeowLiquidGlassSurface()
    {
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16) // ~60fps
        };
        _timer.Tick += OnAnimationTick;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _timer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _timer.Stop();
        base.OnDetachedFromVisualTree(e);
    }

    // ---------- 基准尺寸捕获 ----------

    private void EnsureBaseSize()
    {
        if (_baseCaptured) return;

        _baseWidth = (!double.IsNaN(Width) && Width > 0) ? Width
                   : (Bounds.Width > 0 ? Bounds.Width : 200);

        _baseHeight = (!double.IsNaN(Height) && Height > 0) ? Height
                    : (Bounds.Height > 0 ? Bounds.Height : 200);

        _baseCaptured = true;
    }

    // ---------- 渲染 ----------

    public override void Render(DrawingContext context)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        var topLeft = this.TranslatePoint(new Point(0, 0), topLevel!);

        var operation = new MeowLiquidGlassDrawOperation(true);
        operation.Bounds = new Rect(Bounds.Size);
        operation.WindowOffset = topLeft ?? default;
        operation.RenderScaling = topLevel?.RenderScaling ?? 1.0;
        operation.BlurRadius = BlurRadius;
        operation.BlurredEdgePercent = BlurredEdgePercent;
        operation.CornerRadius = CornerRadius;

        context.Custom(operation);
        base.Render(context);
    }

    // ---------- 指针交互 ----------

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        EnsureBaseSize();

        _monitorPointerMove = true;
        _lastTimeSec = 0;                    // 重置计时，避免旧时间戳
        _smoothedVelocityX = 0;
        _smoothedVelocityY = 0;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _monitorPointerMove = false;

        // 目标回归 1.0，由定时器平滑回弹
        _targetScaleX = MaxScale;
        _targetScaleY = MaxScale;
        _smoothedVelocityX = 0;
        _smoothedVelocityY = 0;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        if (!_monitorPointerMove || !EnableMotionScale)
            return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null)
            return;

        // ★ 关键：用 TopLevel 坐标，控件缩放不会反馈到速度计算里
        var position = e.GetPosition(topLevel);
        var nowSec = _stopwatch.Elapsed.TotalSeconds;

        if (_lastTimeSec > 0)
        {
            var dt = nowSec - _lastTimeSec;

            // 太短的时间间隔噪声极大，直接跳过累积到下一次
            if (dt > 0.004)
            {
                var vx = (position.X - _lastPosition.X) / dt;
                var vy = (position.Y - _lastPosition.Y) / dt;

                // ★ EMA 平滑速度
                _smoothedVelocityX += (vx - _smoothedVelocityX) * VelocityEmaAlpha;
                _smoothedVelocityY += (vy - _smoothedVelocityY) * VelocityEmaAlpha;

                _targetScaleX = ComputeScale(Math.Abs(_smoothedVelocityX));
                _targetScaleY = ComputeScale(Math.Abs(_smoothedVelocityY));
            }
        }

        _lastPosition = position;
        _lastTimeSec = nowSec;
    }

    // ---------- 动画帧：平滑逼近目标缩放 ----------

    private void OnAnimationTick(object? sender, EventArgs e)
    {
        // ★ 每帧只朝目标靠拢一点点，消除抖动
        _currentScaleX += (_targetScaleX - _currentScaleX) * ScaleEmaAlpha;
        _currentScaleY += (_targetScaleY - _currentScaleY) * ScaleEmaAlpha;

        // 差异足够小就不更新了，避免无意义的重排
        var dx = Math.Abs(_currentScaleX - _targetScaleX);
        var dy = Math.Abs(_currentScaleY - _targetScaleY);

        if (_baseCaptured && (dx > 0.001 || dy > 0.001))
        {
            Width  = _baseWidth  * _currentScaleX;
            Height = _baseHeight * _currentScaleY;
        }
    }

    // ---------- 缩放计算 ----------

    private double ComputeScale(double speed)
    {
        var min = MinScale;
        var max = MaxScale;
        var v0 = Math.Max(1e-6, CharacteristicSpeed);

        var t = 1.0 / (1.0 + speed / v0);
        return min + (max - min) * t;
    }
}