using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Meow.LiquidGlass.Avalonia;

public class MeowLiquidGlassSurface : ContentControl
{
    public override void Render(DrawingContext context)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        var topLeft = this.TranslatePoint(new Point(0, 0), topLevel!);
        
        var operation = new MeowLiquidGlassDrawOperation();
        operation.Bounds = new Rect(Bounds.Size);
        operation.WindowOffset = topLeft ?? default;
        operation.RenderScaling = topLevel?.RenderScaling ?? 1.0;

        context.Custom(operation);
        base.Render(context);

    }
}