using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Meow.LiquidGlass.Avalonia;

public class MeowLiquidGlassSurface : ContentControl
{
    public static readonly StyledProperty<float> BlurRadiusProperty = AvaloniaProperty.Register<MeowLiquidGlassSurface, float>(
        nameof(BlurRadius), 3f);

    public static readonly StyledProperty<float> BlurredEdgeProperty = AvaloniaProperty.Register<MeowLiquidGlassSurface, float>(
        nameof(BlurredEdgePercent), 0.15f);

    public float BlurredEdgePercent
    {
        get => GetValue(BlurredEdgeProperty);
        set => SetValue(BlurredEdgeProperty, value);
    }

    public float BlurRadius
    {
        get => GetValue(BlurRadiusProperty);
        set => SetValue(BlurRadiusProperty, value);
    }
    
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
}