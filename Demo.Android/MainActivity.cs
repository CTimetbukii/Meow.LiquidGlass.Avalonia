using Android.App;
using Android.Content.PM;
using Avalonia.Android;

namespace Demo.Android;

[Activity(
    Label = "Meow Liquid Glass",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@mipmap/appicon",
    RoundIcon = "@mipmap/appicon_round",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation
                           | ConfigChanges.ScreenSize
                           | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity
{
}
