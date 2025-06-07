using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace JinReporter_Avalonia;

public partial class App : Application
{
    public override void Initialize()
    {
        // 注册编码提供程序
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
