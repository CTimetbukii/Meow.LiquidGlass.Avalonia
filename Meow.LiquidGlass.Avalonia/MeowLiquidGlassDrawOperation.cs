using Avalonia;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;

namespace Meow.LiquidGlass.Avalonia;

public class MeowLiquidGlassDrawOperation : ICustomDrawOperation
{
    public void Dispose()
    {
        // 在此释放托管资源
    }

    public bool Equals(ICustomDrawOperation? other)
    {
        if (other is not MeowLiquidGlassDrawOperation operation)
        {
            return false;
        }

        return operation.Bounds.Equals(Bounds)
               && operation.WindowOffset.Equals(WindowOffset)
               && operation.RenderScaling.Equals(RenderScaling);
    }

    public bool HitTest(Point p) => Bounds.Contains(p);

    private const string SkSlCode = """
                                    uniform float2 uResolution;
                                    uniform shader uBackground;
                                    uniform float edge;
                                    uniform float2 point_1;
                                    uniform float2 point_2;
                                    uniform float invR; // 必须满足 invR = 1.0 / edge

                                    float refract_offset(float x) {
                                        const float c    = 0.6666666667; // 2/3
                                        const float c2   = 0.4444444444; // (2/3)^2
                                        const float omc2 = 0.5555555556; // 1 - c^2 = 5/9

                                        float s  = (edge - x) * invR;
                                        float s2 = s * s;

                                        float C = sqrt(1.0 - s2);        // cos(r)
                                        float D = sqrt(1.0 - c2 * s2);   // cos(o)

                                        // tan(r-o) = s * (1 - c^2) / (C + c*D)
                                        // offset = edge * (1 + C) * tan(r-o)
                                        return edge * (1.0 + C) * s * omc2 / (C + c * D);
                                    }

                                    half4 main(float2 coord) {
                                        if (coord.x > edge && coord.x < uResolution.x - edge &&
                                            coord.y > edge && coord.y < uResolution.y - edge) {
                                            return uBackground.eval(coord);
                                        }

                                        float tx = sign(point_1.x - coord.x) + sign(point_2.x - coord.x);
                                        float ty = sign(point_1.y - coord.y) + sign(point_2.y - coord.y);

                                        float2 finalCoord = coord;

                                        if (tx != 0.0) {
                                            float stx = sign(tx);
                                            float sx = (stx > 0.0) ? coord.x : uResolution.x - coord.x;
                                            finalCoord.x += stx * refract_offset(sx);
                                        }

                                        if (ty != 0.0) {
                                            float sty = sign(ty);
                                            float sy = (sty > 0.0) ? coord.y : uResolution.y - coord.y;
                                            finalCoord.y += sty * refract_offset(sy);
                                        }

                                        return uBackground.eval(finalCoord);
                                    }
                                    """;

    public Point WindowOffset { get; set; }
    public double RenderScaling { get; set; } = 1.0;

    private readonly SKRuntimeEffect? _effect;

    public MeowLiquidGlassDrawOperation()
    {
        _effect = SKRuntimeEffect.CreateShader(SkSlCode, out var errors);
        if (_effect is null || errors is not null)
        {
            //TODO 处理错误
            Console.WriteLine(errors);
        }
    }

    public void Render(ImmediateDrawingContext context)
    {
        try
        {
            var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (leaseFeature is null) return;

            using var lease = leaseFeature.Lease();
            var canvas = lease.SkCanvas;
            var surface = lease.SkSurface;

            if (surface is null) return;

            // 1. 直接拿全屏快照（不要在中间插一脚CPU降采样）
            using var backgroundImg = surface.Snapshot();

            var s = (float)RenderScaling;
            // 2. 恢复原来的矩阵逻辑（不要在矩阵里乘以 0.5）
            var matrix = SKMatrix.CreateScale(1f / s, 1f / s);
            matrix.TransX = -(float)WindowOffset.X;
            matrix.TransY = -(float)WindowOffset.Y;

            using var backgroundShader = backgroundImg.ToShader(
                SKShaderTileMode.Clamp,
                SKShaderTileMode.Clamp,
                matrix);

            // ... 准备传入 SKSL 的参数 (edge, point_1, point_2, uResolution 保持不变) ...
            var edge = Math.Min(Bounds.Height, Bounds.Width) * 0.15;
            var invR = 1 / edge;

            var uniforms = new SKRuntimeEffectUniforms(_effect);
            uniforms["uResolution"] = new[] { (float)Bounds.Width, (float)Bounds.Height };
            uniforms["invR"] = (float)invR;
            uniforms["edge"] = (float)edge;
            uniforms["point_1"] = new[] { (float)edge, (float)edge };
            uniforms["point_2"] = new[] { (float)(Bounds.Width - edge), (float)(Bounds.Height - edge) };

            var children = new SKRuntimeEffectChildren(_effect);
            children["uBackground"] = backgroundShader;

            using var finalShader = _effect!.ToShader(uniforms, children);
            using var paint = new SKPaint();
            paint.Shader = finalShader;

            // Skia 会自动对全屏高斯模糊进行降采样和升采样优化
            using var filter = SKImageFilter.CreateBlur(3, 3, SKShaderTileMode.Clamp);
            paint.ImageFilter = filter;

            canvas.Save();
            var size = new SKSize((float)Bounds.Width, (float)Bounds.Height);
            var radius = size.Height * 0.15f;
            var rect = SKRect.Create(size.Width, size.Height);
            var maxRadius = Math.Min(rect.Width, rect.Height) * 0.5f;
            radius = Math.Min(radius, maxRadius);

            using var roundRect = new SKRoundRect(rect, radius);
            using var clipPath = new SKPath();
            clipPath.AddRoundRect(roundRect);

            canvas.ClipPath(clipPath, antialias: true);
            canvas.DrawRect(SKRect.Create((float)Bounds.Width, (float)Bounds.Height), paint);
            canvas.Restore();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public Rect Bounds { get; set; }
}