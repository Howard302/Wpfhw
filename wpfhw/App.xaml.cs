using System.Windows;

namespace wpfhw
{
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 显示启动画面
            var splash = new SplashScreen();
            splash.Show();
        }
    }
}