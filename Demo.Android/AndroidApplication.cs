using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;

namespace Demo.Android;

/// <summary>
/// Android 应用入口（对应 AndroidManifest 中 android:name）。
/// Avalonia 12 起 AppBuilder 在这里创建，而不是在 Activity 里。
/// </summary>
[Application(
    Label = "Meow Liquid Glass",
    Icon = "@mipmap/appicon",
    Theme = "@style/MyTheme.NoActionBar")]
public class AndroidApplication : AvaloniaAndroidApplication<App>
{
    protected AndroidApplication(nint javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return base.CustomizeAppBuilder(builder)
            .WithInterFont();
    }
}
