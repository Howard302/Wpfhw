using System.Windows;
using System.Windows.Threading;

namespace wpfhw;

public partial class SplashScreen : Window
{
    public SplashScreen()
    {
        InitializeComponent();

        // 加载完成后等待2秒，自动进入主界面
        Loaded += (s, e) =>
        {
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            timer.Tick += (sender, args) =>
            {
                timer.Stop();

                // 创建并显示主窗口
                var mainWindow = new MainWindow();
                mainWindow.Show();

                // 关闭启动画面
                Close();
            };
            timer.Start();
        };
    }
}