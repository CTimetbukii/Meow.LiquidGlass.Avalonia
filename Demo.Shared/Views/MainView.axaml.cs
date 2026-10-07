using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Demo.ViewModels;

namespace Demo.Views;

/// <summary>
/// 移动端（Android）根视图：与桌面版 MainWindow 同一套交互，
/// 但根节点是 UserControl 而不是 Window。
/// </summary>
public partial class MainView : UserControl
{
    private readonly TranslateTransform _dragOffset = new();

    private bool _isDragging;
    private Point _dragStartPoint;
    private double _dragStartX;
    private double _dragStartY;
    private Control? _draggingControl;

    public MainView()
    {
        InitializeComponent();

        DataContext = new MainWindowViewModel();

        // 把 TranslateTransform 挂到 Glass 控件上
        Glass.RenderTransform = _dragOffset;
    }

    private void OnGlassPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control control)
        {
            return;
        }

        // 触屏上 PointerPressed 本身带左键语义，这里不再强制要求 LeftButton，
        // 以便手指触摸也能拖动。
        _isDragging = true;
        _draggingControl = control;

        _dragStartPoint = e.GetPosition(this);

        _dragStartX = _dragOffset.X;
        _dragStartY = _dragOffset.Y;

        e.Pointer.Capture(control);
        e.Handled = true;
    }

    private void OnGlassPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDragging || _draggingControl is null)
        {
            return;
        }

        var current = e.GetPosition(this);
        double dx = current.X - _dragStartPoint.X;
        double dy = current.Y - _dragStartPoint.Y;

        _dragOffset.X = _dragStartX + dx;
        _dragOffset.Y = _dragStartY + dy;
    }

    private void OnGlassPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        _isDragging = false;
        _draggingControl = null;
        e.Pointer.Capture(null);
    }
}
