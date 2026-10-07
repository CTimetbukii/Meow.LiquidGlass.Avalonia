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
                                    uniform float invR;
                                    uniform float edge;
                                    uniform float right_x_edge;
                                    uniform float right_y_edge;
                                    
                                    vec4 left_top(float2 coord, float R);
                                    vec4 top(float2 coord, float R);
                                    vec4 right_top(float2 coord, float R);
                                    vec4 right(float2 coord, float R);
                                    vec4 right_bottom(float2 coord, float R);
                                    vec4 bottom(float2 coord, float R);
                                    vec4 left_bottom(float2 coord, float R);
                                    vec4 left(float2 coord, float R);
                                    float refract_once(float x, float R);
                                    float refract_twice(float x, float R);
                                    
                                    half4 main(float2 coord) {
                                        // 先按 y 分三行
                                        if (coord.y < edge) {
                                            // 上边一行
                                            if (coord.x < edge) return left_top(coord, edge);
                                            if (coord.x > right_x_edge) return right_top(coord, edge);
                                            return top(coord, edge);
                                        }
                                    
                                        if (coord.y > right_y_edge) {
                                            // 下边一行
                                            if (coord.x < edge) return left_bottom(coord, edge);
                                            if (coord.x > right_x_edge) return right_bottom(coord, edge);
                                            return bottom(coord, edge);
                                        }
                                    
                                        // 中间一行
                                        if (coord.x < edge) return left(coord, edge);
                                        if (coord.x > right_x_edge) return right(coord, edge);
                                        return uBackground.eval(coord);
                                    }
                                    
                                    vec4 left_top(float2 coord, float R) {
                                        // 该方法分别计算x和y的分量结果，然后通过向量处理得到最终颜色映射结果
                                        // 首先判断该使用1次折射还是2次折射，计算出判断边界
                                        // 这个 0.007843258 * R 其实是 (1-(3√7/8)R) 的近似结果。0.007843258是1-(3√7/8)的近似结果，已经贴近float的精度极限
                                        // 这个 (1-(3√7/8)R) 是根据几何学，光学计算出的折射边界。超过该边界时光线将折射1次，未超过时则折射2次
                                        float refract_edge = 0.007843258 * R;
                                    
                                        float x_refract_result;
                                        float y_refract_result;
                                    
                                        if (coord.x > refract_edge) {
                                            x_refract_result = refract_once(coord.x, R);
                                        } else {
                                            x_refract_result = refract_twice(coord.x, R);
                                        }
                                    
                                        if (coord.y > refract_edge) {
                                            y_refract_result = refract_once(coord.y, R);
                                        } else {
                                            y_refract_result = refract_twice(coord.y, R);
                                        }
                                    
                                        // 向量结合，返回对应位置的颜色
                                        return uBackground.eval(float2(x_refract_result, y_refract_result));
                                    }
                                    
                                    vec4 top(float2 coord, float R) {
                                        // 该方法忽略x的分量结果
                                        float refract_edge = 0.007843258 * R;
                                    
                                        float y_refract_result;
                                    
                                        if (coord.y > refract_edge) {
                                            y_refract_result = refract_once(coord.y, R);
                                        } else {
                                            y_refract_result = refract_twice(coord.y, R);
                                        }
                                    
                                        return uBackground.eval(float2(coord.x, y_refract_result));
                                    }
                                    
                                    vec4 right_top(float2 coord, float R) {
                                        // 该方法同时计算x 和y的分量，但x的值需要经过特殊处理
                                        float x = uResolution.x - coord.x;
                                    
                                        // 同时处理2个分量
                                        float refract_edge = 0.007843258 * R;
                                    
                                        float x_refract_result;
                                        float y_refract_result;
                                    
                                        if (x > refract_edge) {
                                            x_refract_result = refract_once(x, R);
                                        } else {
                                            x_refract_result = refract_twice(x, R);
                                        }
                                    
                                        if (coord.y > refract_edge) {
                                            y_refract_result = refract_once(coord.y, R);
                                        } else {
                                            y_refract_result = refract_twice(coord.y, R);
                                        }
                                    
                                        //在返回结果的时候，要重新把x处理回去
                                        return uBackground.eval(float2(uResolution.x - x_refract_result, y_refract_result));
                                    }
                                    
                                    vec4 right(float2 coord, float R) {
                                        // 该方法忽略y的分量，并对x进行特殊处理
                                        
                                        float x = uResolution.x - coord.x;
                                    
                                        float refract_edge = 0.007843258 * R;
                                    
                                        float x_refract_result;
                                    
                                        if (x > refract_edge) {
                                            x_refract_result = refract_once(x, R);
                                        } else {
                                            x_refract_result = refract_twice(x, R);
                                        }
                                    
                                        //返回结果的时候，把x处理回去
                                        return uBackground.eval(float2(uResolution.x - x_refract_result, coord.y));
                                    }
                                    
                                    vec4 right_bottom(float2 coord, float R) {
                                        //该方法同时特殊处理x和y
                                        float x = uResolution.x - coord.x;
                                        float y = uResolution.y - coord.y;
                                    
                                        float refract_edge = 0.007843258 * R;
                                    
                                        float x_refract_result;
                                        float y_refract_result;
                                    
                                        if (x > refract_edge) {
                                            x_refract_result = refract_once(x, R);
                                        } else {
                                            x_refract_result = refract_twice(x, R);
                                        }
                                    
                                        if (y > refract_edge) {
                                            y_refract_result = refract_once(y, R);
                                        } else {
                                            y_refract_result = refract_twice(y, R);
                                        }
                                    
                                        // 特殊处理
                                        return uBackground.eval(float2(uResolution.x - x_refract_result, uResolution.y - y_refract_result));
                                    }
                                    
                                    vec4 bottom(float2 coord, float R) {
                                        //特殊处理y，忽略x的分量
                                        float y = uResolution.y - coord.y;
                                    
                                        float refract_edge = 0.007843258 * R;
                                        
                                        float y_refract_result;
                                    
                                        if (y > refract_edge) {
                                            y_refract_result = refract_once(y, R);
                                        } else {
                                            y_refract_result = refract_twice(y, R);
                                        }
                                    
                                        return uBackground.eval(float2(coord.x, uResolution.y - y_refract_result));
                                    }
                                    
                                    vec4 left_bottom(float2 coord, float R) {
                                        //特殊处理y，同时处理2个分量
                                        float y = uResolution.y - coord.y;
                                    
                                        float refract_edge = 0.007843258 * R;
                                        
                                        float x_refract_result;
                                        float y_refract_result;
                                    
                                        if (coord.x > refract_edge) {
                                            x_refract_result = refract_once(coord.x, R);
                                        } else {
                                            x_refract_result = refract_twice(coord.x, R);
                                        }
                                    
                                        if (y > refract_edge) {
                                            y_refract_result = refract_once(y, R);
                                        } else {
                                            y_refract_result = refract_twice(y, R);
                                        }
                                    
                                        return uBackground.eval(float2(x_refract_result, uResolution.y - y_refract_result));
                                    }
                                    
                                    vec4 left(float2 coord, float R) {
                                        //忽略y分量
                                        
                                        float refract_edge = 0.007843258 * R;
                                    
                                        float x_refract_result;
                                        
                                        if (coord.x > refract_edge) {
                                            x_refract_result = refract_once(coord.x, R);
                                        } else {
                                            x_refract_result = refract_twice(coord.x, R);
                                        }
                                    
                                        return uBackground.eval(float2(x_refract_result, coord.y));
                                    }
                                    
                                    float refract_once(float x, float R) {
                                        // //计算sinr
                                        // float sinr = (R - x) / R; //性能开销。可以在CPU侧提前计算好1/R
                                    
                                        // //根据折射定律计算sino，取玻璃n=1.5
                                        // float sino = sinr * 0.6666666;
                                    
                                        // //计算出cosr，coso
                                        // float cosr = sqrt(1-(sinr * sinr)); //性能开销，sqrt
                                        // float coso = sqrt(1-(sino * sino)); //性能开销, sqrt
                                    
                                        // //使用三角函数展开，计算tan(r-o)
                                        // float tanro = (sinr * coso - cosr * sino) / (cosr * coso + sinr * sino);
                                    
                                        // //计算tanr
                                        // float tanr = sinr / cosr;
                                    
                                        // //计算h
                                        // float h = R + (R - x) / tanr;
                                    
                                        // //得到初步映射偏移量
                                        // float refract_offset = h * tanro;
                                    
                                        // return x + refract_offset;
                                    
                                        
                                        // invR 建议在 CPU 侧预计算：invR = 1.0 / R
                                        float s = (R - x) * invR;   // sinr
                                        float s2 = s * s;
                                    
                                        float cosr = sqrt(1.0 - s2);
                                        float coso = sqrt(1.0 - 0.4444444444 * s2); // 0.4444444444 = (2/3)^2
                                    
                                        float tanro = s * (coso - 0.6666666667 * cosr)
                                                    / (cosr * coso + 0.6666666667 * s2);
                                    
                                        float h = R * (1.0 + cosr); // 等价于原 h = R + (R - x) / tanr
                                        return x + h * tanro;
                                    }
                                    
                                    float refract_twice(float x, float R) {
                                        // //计算sinr
                                        // float sinr = (R - x) / R;
                                    
                                        // //折射定律, n取1.5
                                        // float sino = sinr / 1.5;
                                    
                                        // //计算cosr, coso
                                        // float cosr = sqrt(1-(sinr * sinr));
                                        // float coso = sqrt(1-(sino * sino));
                                    
                                        // //计算h
                                        // float h = R * cosr;
                                    
                                        // //根据正弦定律，计算H
                                        // float H = (R * coso) / (coso * cosr + sino * sinr);
                                    
                                        // //计算P
                                        // float P = R + h - H;
                                    
                                        // //计算sin(r-o)，cos(r-o)，为了计算下面的tan(2r-2o)
                                        // float sinro = sinr * sino - cosr * sino;
                                        // float cosro = cosr * coso + sinr * sino;
                                    
                                        // //计算tan(2r-2o)，利用三角函数展开计算
                                        // float tan2r2o = (2 * sinro * cosro) / (cosro * cosro - sinro * sinro);
                                    
                                        // //最后得到偏移量
                                        // float refract_offset = P * tan2r2o;
                                    
                                        // return x + refract_offset;
                                    
                                        // 常量：n=1.5
                                        const float k  = 0.6666666667; // 1/1.5
                                        const float k2 = 0.4444444444; // (1/1.5)^2 = 4/9
                                    
                                        // sinr = (R - x) / R = (R - x) * invR
                                        float s = (R - x) * invR;
                                        float s2 = s * s;
                                    
                                        // cosr, coso
                                        float cr = sqrt(1.0 - s2);
                                        float co = sqrt(1.0 - k2 * s2);
                                    
                                        // 计算 cos(r-o) 和 sin(r-o)
                                        // cos(r-o) = cosr*coso + sinr*sino = cr*co + k*s2
                                        // sin(r-o) = sinr*coso - cosr*sino = s*(co - k*cr)
                                        float cosro = cr * co + k * s2;
                                        float sinro = s * (co - k * cr);
                                    
                                        // H = R * coso / cos(r-o)
                                        // P = R + h - H = R + R*cr - R*co/cosro = R*(1 + cr - co/cosro)
                                        float P = R * (1.0 + cr - co / cosro);
                                    
                                        // tan(2(r-o)) = 2*sin(r-o)*cos(r-o) / (cos²(r-o) - sin²(r-o))
                                        float tan2r2o = (2.0 * sinro * cosro) / (cosro * cosro - sinro * sinro);
                                    
                                        // 最终偏移
                                        float refract_offset = P * tan2r2o;
                                    
                                        return x + refract_offset;
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
            if (leaseFeature is null)
            {
                Console.WriteLine(1);
                return;
            }

            using var lease = leaseFeature.Lease();

            var canvas = lease.SkCanvas;
            var surface = lease.SkSurface;

            if (surface is null)
            {
                Console.WriteLine(2);
                return;
            }
            
            using var backgroundImg = surface.Snapshot();

            var s = (float)RenderScaling;
            var matrix = SKMatrix.CreateScale(1f / s, 1f / s);
            matrix.TransX = -(float)WindowOffset.X;
            matrix.TransY = -(float)WindowOffset.Y;

            using var backgroundShader = backgroundImg.ToShader(
                SKShaderTileMode.Clamp,
                SKShaderTileMode.Clamp,
                matrix);

            // 准备传入SKSL的参数
            
            // 提前计算edge，1/R，避免GPU去算这个
            var edge = Math.Min(Bounds.Height, Bounds.Width) * 0.15;
            var invR = 1 / edge;
            var rightXEdge = Bounds.Width - edge;
            var rightYEdge = Bounds.Height - edge;
            
            var uniforms = new SKRuntimeEffectUniforms(_effect);
            uniforms["uResolution"] = new[] { (float)Bounds.Width, (float)Bounds.Height };
            uniforms["invR"] = (float)invR;
            uniforms["edge"] = (float)edge;
            uniforms["right_x_edge"] = (float)rightXEdge;
            uniforms["right_y_edge"] = (float)rightYEdge;
            //right_x_edge

            var children = new SKRuntimeEffectChildren(_effect);
            children["uBackground"] = backgroundShader;

            // 生成最终的SKShader并使用SKPaint绘制到Canvas上
            using var finalShader = _effect!.ToShader(uniforms, children);
            using var paint = new SKPaint();
            paint.Shader = finalShader;
            
            //加个高斯模糊
            using var filter = SKImageFilter.CreateBlur(2, 2, SKShaderTileMode.Clamp);
            paint.ImageFilter = filter;

            //将绘制限制在控件的边界内
            canvas.Save();

            var size = new SKSize((float)Bounds.Width, (float)Bounds.Height);
            
            // 圆角半径 = 高的 20%
            var radius = size.Height * 0.15f;

            // 目标矩形（假设裁整个 canvas）
            var rect = SKRect.Create(size.Width, size.Height);

            // 关键：圆角半径不能超过宽/高的一半，否则 Skia 会自动缩放
            var maxRadius = Math.Min(rect.Width, rect.Height) * 0.5f;
            radius = Math.Min(radius, maxRadius);

            using var roundRect = new SKRoundRect(rect, radius);
            using var clipPath = new SKPath();
            clipPath.AddRoundRect(roundRect);

            canvas.ClipPath(clipPath, antialias: true);
            canvas.DrawRect(SKRect.Create((float)Bounds.Width, (float)Bounds.Height), paint);

            canvas.ClipRect(Bounds.ToSKRect());
            canvas.DrawRect(Bounds.ToSKRect(), paint);
            canvas.Restore();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public Rect Bounds { get; set; }
}