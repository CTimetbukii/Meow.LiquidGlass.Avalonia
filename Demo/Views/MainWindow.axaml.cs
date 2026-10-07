using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Demo.Views;

public partial class MainWindow : Window
{
    private readonly TranslateTransform _dragOffset = new();

    private bool _isDragging;
    private Point _dragStartPoint;
    private double _dragStartX;
    private double _dragStartY;
    private Control? _draggingControl;

    public MainWindow()
    {
        InitializeComponent();

        // 把 TranslateTransform 挂到 Glass 控件上
        Glass.RenderTransform = _dragOffset;
    }

    private void OnGlassPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control control)
        {
            return;
        }

        var point = e.GetCurrentPoint(control);
        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        _isDragging = true;
        _draggingControl = control;

        _dragStartPoint = e.GetPosition(this);

        // 从 TranslateTransform 读起始偏移
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