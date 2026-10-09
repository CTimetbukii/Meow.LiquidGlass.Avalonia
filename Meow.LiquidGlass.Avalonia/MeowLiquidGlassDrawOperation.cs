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

    private const string SkSlCodePoorQuality = """
                                    uniform float2 uResolution;
                                    uniform shader uBackground;
                                    uniform float edge;
                                    uniform float2 point_1;
                                    uniform float2 point_2;
                                    uniform float invR; // 必须等于 1.0 / edge
                                    
                                    // 返回的是折射偏移量，不是最终坐标
                                    float refract_offset(float x) {
                                        const float c    = 0.6666666667; // 2/3
                                        const float c2   = 0.4444444444; // (2/3)^2
                                        const float omc2 = 0.5555555556; // 1 - c^2 = 5/9
                                    
                                        // 防止 stx=0 时 sx 落在无效区域导致 sqrt 负数
                                        float s = clamp((edge - x) * invR, 0.0, 1.0);
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
                                    
                                        float stx = sign(tx);
                                        float sty = sign(ty);
                                    
                                        // 纯数学镜像，无三元：
                                        // stx =  1 -> sx = coord.x
                                        // stx = -1 -> sx = uResolution.x - coord.x
                                        // stx =  0 -> sx = 0.5 * uResolution.x （后面乘 stx=0 会抵消）
                                        float sx = 0.5 * (1.0 - stx) * uResolution.x + stx * coord.x;
                                        float sy = 0.5 * (1.0 - sty) * uResolution.y + sty * coord.y;
                                    
                                        float offx = refract_offset(sx);
                                        float offy = refract_offset(sy);
                                    
                                        // 统一为：coord + sign * offset
                                        float2 finalCoord = coord + float2(stx * offx, sty * offy);
                                    
                                        return uBackground.eval(finalCoord);
                                    }
                                    """;
    
    private const string SkSlCodeHighQuality = """
                                               uniform float2 uResolution;
                                               uniform shader uBackground;
                                               uniform float edge;
                                               uniform float2 point_1;
                                               uniform float2 point_2;
                                               uniform float invR; // 必须等于 1.0 / edge
                                               
                                               // 一次折射：数学化简版
                                               float refract_once(float x) {
                                                   float R = edge;
                                                   const float c    = 0.6666666667; // 2/3
                                                   const float c2   = 0.4444444444; // (2/3)^2
                                                   const float omc2 = 0.5555555556; // 1 - c^2 = 5/9
                                               
                                                   float s  = (R - x) * invR; // sinr
                                                   float s2 = s * s;
                                               
                                                   float C = sqrt(1.0 - s2);      // cosr
                                                   float D = sqrt(1.0 - c2 * s2); // coso
                                               
                                                   // tan(r-o) = s * (1 - c^2) / (C + c*D)
                                                   // offset = R * (1 + C) * tan(r-o)
                                                   return x + R * (1.0 + C) * s * omc2 / (C + c * D);
                                               }
                                               
                                               // 二次折射：合并除法版
                                               float refract_twice(float x) {
                                                   float R = edge;
                                                   const float k  = 0.6666666667; // 2/3
                                                   const float k2 = 0.4444444444; // (2/3)^2
                                               
                                                   float s  = (R - x) * invR; // sinr
                                                   float s2 = s * s;
                                               
                                                   float cr = sqrt(1.0 - s2);      // cosr
                                                   float co = sqrt(1.0 - k2 * s2); // coso
                                               
                                                   // cos(r-o) = cr*co + k*s2
                                                   // sin(r-o) = s*(co - k*cr)
                                                   float cosro = cr * co + k * s2;
                                                   float sinro = s * (co - k * cr);
                                               
                                                   // 原式：
                                                   // P = R * (1 + cr - co/cosro)
                                                   // tan2r2o = 2*sinro*cosro / (cosro^2 - sinro^2)
                                                   // offset = P * tan2r2o
                                                   //
                                                   // 合并后：
                                                   // offset = R * 2*sinro * ((1+cr)*cosro - co) / (cosro^2 - sinro^2)
                                                   float num = 2.0 * sinro * ((1.0 + cr) * cosro - co);
                                                   float den = cosro * cosro - sinro * sinro;
                                               
                                                   return x + R * num / den;
                                               }
                                               
                                               half4 main(float2 coord) {
                                                   if (coord.x > edge && coord.x < uResolution.x - edge &&
                                                       coord.y > edge && coord.y < uResolution.y - edge) {
                                                       return uBackground.eval(coord);
                                                   }
                                               
                                                   float x_f1 = point_1.x - coord.x;
                                                   float x_f2 = point_2.x - coord.x;
                                                   float y_f1 = point_1.y - coord.y;
                                                   float y_f2 = point_2.y - coord.y;
                                               
                                                   float tx = sign(x_f1) + sign(x_f2);
                                                   float ty = sign(y_f1) + sign(y_f2);
                                               
                                                   float stx = sign(tx);
                                                   float sty = sign(ty);
                                               
                                                   // 距离对应边缘的距离
                                                   // stx =  1 -> sx = coord.x
                                                   // stx = -1 -> sx = uResolution.x - coord.x
                                                   // stx =  0 -> sx = 0.5 * uResolution.x （后面乘 stx=0 会抵消）
                                                   float sx = 0.5 * (1.0 - stx) * uResolution.x + stx * coord.x;
                                                   float sy = 0.5 * (1.0 - sty) * uResolution.y + sty * coord.y;
                                               
                                                   float refract_edge = 0.007843258 * edge;
                                               
                                                   float x_off = 0.0;
                                                   float y_off = 0.0;
                                               
                                                   if (tx != 0.0) {
                                                       if (sx >= refract_edge) {
                                                           x_off = refract_once(sx);
                                                       } else {
                                                           x_off = refract_twice(sx);
                                                       }
                                                   }
                                               
                                                   if (ty != 0.0) {
                                                       if (sy >= refract_edge) {
                                                           y_off = refract_once(sy);
                                                       } else {
                                                           y_off = refract_twice(sy);
                                                       }
                                                   }
                                               
                                                   // 直接还原为绝对坐标：
                                                   // stx =  1 -> final_x = coord.x + (x_off - sx) = x_off
                                                   // stx = -1 -> final_x = coord.x - (x_off - sx) = uResolution.x - x_off
                                                   // stx =  0 -> final_x = coord.x
                                                   float final_x = coord.x + stx * (x_off - sx);
                                                   float final_y = coord.y + sty * (y_off - sy);
                                               
                                                   return uBackground.eval(float2(final_x, final_y));
                                               }
                                               """;
    
    private const string SkSlCodeBright = """
                                          uniform float2 uResolution;
                                          uniform shader uBackground;
                                          uniform float edge;
                                          uniform float2 point_1;
                                          uniform float2 point_2;
                                          uniform float invR; // 必须等于 1.0 / edge
                                          
                                          // 一次折射：数学化简版
                                          float refract_once(float x) {
                                              float R = edge;
                                              const float c    = 0.6666666667; // 2/3
                                              const float c2   = 0.4444444444; // (2/3)^2
                                              const float omc2 = 0.5555555556; // 1 - c^2 = 5/9
                                          
                                              float s  = (R - x) * invR; // sinr
                                              float s2 = s * s;
                                          
                                              float C = sqrt(1.0 - s2);      // cosr
                                              float D = sqrt(1.0 - c2 * s2); // coso
                                          
                                              // tan(r-o) = s * (1 - c^2) / (C + c*D)
                                              // offset = R * (1 + C) * tan(r-o)
                                              return x + R * (1.0 + C) * s * omc2 / (C + c * D);
                                          }
                                          
                                          // 二次折射：合并除法版
                                          float refract_twice(float x) {
                                              float R = edge;
                                              const float k  = 0.6666666667; // 2/3
                                              const float k2 = 0.4444444444; // (2/3)^2
                                          
                                              float s  = (R - x) * invR; // sinr
                                              float s2 = s * s;
                                          
                                              float cr = sqrt(1.0 - s2);      // cosr
                                              float co = sqrt(1.0 - k2 * s2); // coso
                                          
                                              // cos(r-o) = cr*co + k*s2
                                              // sin(r-o) = s*(co - k*cr)
                                              float cosro = cr * co + k * s2;
                                              float sinro = s * (co - k * cr);
                                          
                                              // 原式：
                                              // P = R * (1 + cr - co/cosro)
                                              // tan2r2o = 2*sinro*cosro / (cosro^2 - sinro^2)
                                              // offset = P * tan2r2o
                                              //
                                              // 合并后：
                                              // offset = R * 2*sinro * ((1+cr)*cosro - co) / (cosro^2 - sinro^2)
                                              float num = 2.0 * sinro * ((1.0 + cr) * cosro - co);
                                              float den = cosro * cosro - sinro * sinro;
                                          
                                              return x + R * num / den;
                                          }
                                          
                                          half3 glassify(half3 c) {
                                              c = c * 1.03;                    // 提亮
                                              c = mix(c, half3(1.0), 0.03);    // 加白，通透
                                              c = (c - 0.5) * 1.04 + 0.5;      // 轻微提对比
                                              return clamp(c, 0.0, 1.0);
                                          }
                                          
                                          half4 main(float2 coord) {
                                              // ---------------- 直通区 ----------------
                                              if (coord.x > edge && coord.x < uResolution.x - edge &&
                                                  coord.y > edge && coord.y < uResolution.y - edge) {
                                          
                                                  half4 bg = uBackground.eval(coord);
                                                  half3 c  = bg.a > 0.0 ? bg.rgb / bg.a : bg.rgb;
                                                  c = glassify(c);
                                                  return half4(c * bg.a, bg.a);
                                              }
                                          
                                              // ---------------- 折射区 ----------------
                                              float x_f1 = point_1.x - coord.x;
                                              float x_f2 = point_2.x - coord.x;
                                              float y_f1 = point_1.y - coord.y;
                                              float y_f2 = point_2.y - coord.y;
                                          
                                              float tx = sign(x_f1) + sign(x_f2);
                                              float ty = sign(y_f1) + sign(y_f2);
                                          
                                              float stx = sign(tx);
                                              float sty = sign(ty);
                                          
                                              float sx = 0.5 * (1.0 - stx) * uResolution.x + stx * coord.x;
                                              float sy = 0.5 * (1.0 - sty) * uResolution.y + sty * coord.y;
                                          
                                              float refract_edge = 0.007843258 * edge;
                                          
                                              float x_off = 0.0;
                                              float y_off = 0.0;
                                          
                                              if (tx != 0.0) {
                                                  if (sx >= refract_edge) {
                                                      x_off = refract_once(sx);
                                                  } else {
                                                      x_off = refract_twice(sx);
                                                  }
                                              }
                                          
                                              if (ty != 0.0) {
                                                  if (sy >= refract_edge) {
                                                      y_off = refract_once(sy);
                                                  } else {
                                                      y_off = refract_twice(sy);
                                                  }
                                              }
                                          
                                              float final_x = coord.x + stx * (x_off - sx);
                                              float final_y = coord.y + sty * (y_off - sy);
                                          
                                              half4 bg = uBackground.eval(float2(final_x, final_y));
                                              half3 c  = bg.a > 0.0 ? bg.rgb / bg.a : bg.rgb;
                                          
                                              // 和直通一样的透亮处理
                                              c = glassify(c);
                                          
                                              // 折射区额外：到四条边最近距离，越靠边越亮
                                              float dx = min(coord.x, uResolution.x - coord.x);
                                              float dy = min(coord.y, uResolution.y - coord.y);
                                              float d  = min(dx, dy);
                                          
                                              // edge 之内算折射带，越往里越接近 0
                                              float t = 1.0 - smoothstep(0.0, edge, d);
                                          
                                              // 边缘加白高光
                                              c = mix(c, half3(1.0), 0.12 * t);
                                          
                                              // 冷色偏移，玻璃味
                                              c.b *= 1.0 + 0.03 * t;
                                              c.r *= 1.0 - 0.02 * t;
                                          
                                              c = clamp(c, 0.0, 1.0);
                                          
                                              return half4(c * bg.a, bg.a);
                                          }
                                          """;

    public Point WindowOffset { get; set; }
    public double RenderScaling { get; set; } = 1.0;
    
    public float BlurredEdgePercent { get; set; }
    public float BlurRadius { get; set; }
    public CornerRadius CornerRadius { get; set; }

    private readonly SKRuntimeEffect? _effect;

    public MeowLiquidGlassDrawOperation(bool useHighQuality)
    {
        string? errs;
        if (useHighQuality)
        {
            _effect = SKRuntimeEffect.CreateShader(SkSlCodeBright, out var errors);
            errs = errors;
        }
        else
        {
            _effect = SKRuntimeEffect.CreateShader(SkSlCodePoorQuality, out var errors);
            errs = errors;
        }
        
        if (_effect is null || errs is not null)
        {
            //TODO 处理错误
            Console.WriteLine(errs);
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
            var edge = Math.Min(Bounds.Height, Bounds.Width) * BlurredEdgePercent;
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
            using var filter = SKImageFilter.CreateBlur(BlurRadius, BlurRadius, SKShaderTileMode.Clamp);
            paint.ImageFilter = filter;

            canvas.Save();
            var size = new SKSize((float)Bounds.Width, (float)Bounds.Height);
            var radius = size.Height * 0.15f; // todo corner radius 调整
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