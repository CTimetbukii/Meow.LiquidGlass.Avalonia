using Avalonia.Controls;
using Avalonia.Input;

namespace Demo.Views;

/// <summary>
/// 桌面主窗口。页面上有两张卡片——纯白对照卡片与液态玻璃卡片，
/// 拖拽逻辑完全相同，用来区分「拖拽本身的开销」和「玻璃着色器的开销」。
/// </summary>
public partial class MainWindow : Window
{
    private readonly DragBehavior _plainDrag;
    private readonly DragBehavior _glassDrag;

    public MainWindow()
    {
        InitializeComponent();

        _plainDrag = new DragBehavior(this, PlainCard);
        _glassDrag = new DragBehavior(this, Glass);
    }

    private void OnPlainPointerPressed(object? sender, PointerPressedEventArgs e)
        => _plainDrag.OnPointerPressed(sender, e);

    private void OnPlainPointerMoved(object? sender, PointerEventArgs e)
        => _plainDrag.OnPointerMoved(sender, e);

    private void OnPlainPointerReleased(object? sender, PointerReleasedEventArgs e)
        => _plainDrag.OnPointerReleased(sender, e);

    private void OnGlassPointerPressed(object? sender, PointerPressedEventArgs e)
        => _glassDrag.OnPointerPressed(sender, e);

    private void OnGlassPointerMoved(object? sender, PointerEventArgs e)
        => _glassDrag.OnPointerMoved(sender, e);

    private void OnGlassPointerReleased(object? sender, PointerReleasedEventArgs e)
        => _glassDrag.OnPointerReleased(sender, e);
}
