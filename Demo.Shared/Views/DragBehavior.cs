using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Demo.Views;

/// <summary>
/// 可拖拽控件助手：把一个 <see cref="TranslateTransform"/> 挂到目标控件上，
/// 并处理按下 / 移动 / 抬起三段指针事件。
/// 桌面的玻璃卡片、纯白对照卡片共用同一份逻辑，保证两者开销完全一致。
/// </summary>
public sealed class DragBehavior
{
    private readonly TranslateTransform _offset = new();
    private readonly Control _owner;

    private bool _isDragging;
    private Point _dragStartPoint;
    private double _dragStartX;
    private double _dragStartY;
    private Control? _draggingControl;

    public DragBehavior(Control owner, Control target)
    {
        _owner = owner;
        target.RenderTransform = _offset;
    }

    public void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control control)
        {
            return;
        }

        // 触屏上 PointerPressed 本身带左键语义，因此不强制要求 LeftButton，
        // 以便鼠标和手指都能拖拽。
        _isDragging = true;
        _draggingControl = control;

        _dragStartPoint = e.GetPosition(_owner);
        _dragStartX = _offset.X;
        _dragStartY = _offset.Y;

        e.Pointer.Capture(control);
        e.Handled = true;
    }

    public void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDragging || _draggingControl is null)
        {
            return;
        }

        var current = e.GetPosition(_owner);
        _offset.X = _dragStartX + (current.X - _dragStartPoint.X);
        _offset.Y = _dragStartY + (current.Y - _dragStartPoint.Y);
    }

    public void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
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
