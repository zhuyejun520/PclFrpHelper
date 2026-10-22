using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PclFrpAddon
{
    public static class Ui
    {
        private static string HelperPath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"PCL\frp\PclFrpHelper.exe");
        }

        private static void EnsureHelper()
        {
            try
            {
                var path = HelperPath();
                var dir = Path.GetDirectoryName(path);
                Directory.CreateDirectory(dir);
                using (var s = typeof(Ui).Assembly.GetManifestResourceStream("PclFrpHelper.exe"))
                {
                    if (s == null) return;
                    if (File.Exists(path) && new FileInfo(path).Length == s.Length) return;
                    using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
                        s.CopyTo(fs);
                }
            }
            catch { }
        }

        private static void Launch()
        {
            try
            {
                EnsureHelper();
                var exe = HelperPath();
                if (File.Exists(exe)) Process.Start(exe);
                else MessageBox.Show("FRP 助手释放失败：" + exe, "FRP 联机", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "FRP 联机", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        public static void Attach(object target)
        {
            try
            {
                var panel = target as Panel;
                if (panel == null) return;
                var host = VisualTreeHelper.GetParent(panel) as Panel;
                if (host == null) host = panel;

                foreach (UIElement c in host.Children)
                {
                    var fe = c as FrameworkElement;
                    if (fe != null && (fe.Tag as string) == "PclFrpPill") return;
                }

                var pill = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(11, 91, 203)),
                    CornerRadius = new CornerRadius(16),
                    Padding = new Thickness(18, 7, 18, 7),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(18, 0, 0, 14),
                    Cursor = Cursors.Hand,
                    Tag = "PclFrpPill",
                    ToolTip = "用 OpenFrp / 樱花FRP / 自建 frps 把本地端口映射到公网",
                    Child = new TextBlock { Text = "FRP 联机", Foreground = Brushes.White, FontWeight = FontWeights.Bold, FontSize = 13 }
                };
                pill.MouseLeftButtonUp += (s, e) => Launch();
                host.Children.Add(pill);

                EnsureHelper();
            }
            catch { }
        }
    }
}