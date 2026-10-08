using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StepCodeUsage
{
    //注册表管理类，实现开机自启功能
    public class StartupManager
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "StepCodeUsage"; // 注册表里的名称，不要随便改

        /// <summary>
        /// 检查是否已经设置了开机自启
        /// </summary>
        public static bool IsStartupEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey, false))
                {
                    if (key == null) return false;
                    var value = key.GetValue(AppName) as string;
                    return !string.IsNullOrEmpty(value);
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 设置或取消开机自启
        /// </summary>
        public static void SetStartup(bool enable)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (key == null) return;

                    if (enable)
                    {
                        // 获取当前 exe 的绝对路径
                        // .NET 6+ 推荐使用 Environment.ProcessPath，它完美支持单文件发布
                        string exePath = Environment.ProcessPath;

                        // 如果是旧版 .NET，用 Process.GetCurrentProcess().MainModule.FileName 作为备选
                        if (string.IsNullOrEmpty(exePath))
                        {
                            exePath = Process.GetCurrentProcess().MainModule?.FileName;
                        }

                        // 加引号防止路径中有空格（如 C:\Program Files\...）
                        key.SetValue(AppName, $"\"{exePath}\"");
                    }
                    else
                    {
                        // 取消开机自启
                        key.DeleteValue(AppName, false);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"设置开机自启动失败: {ex.Message}", "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }
}
