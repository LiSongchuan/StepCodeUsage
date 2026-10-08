using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Threading;

namespace StepCodeUsage
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private DispatcherTimer _countdownTimer;     // 1秒间隔，驱动倒计时和刷新
        private DispatcherTimer _animationTimer;     // 动画定时器
        private bool _isUpdating = false;

        // 刷新间隔（秒）
        private const int RefreshInterval = 60;
        private int _secondsRemaining = RefreshInterval;

        // 历史记录文件路径 (exe 同级目录下)
        private readonly string _historyFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "usage_history.json");
        private Dictionary<string, double> _usageHistory = new Dictionary<string, double>();

        // 圆环相关参数
        private const double Radius = 100;
        private const double StrokeThickness = 18;
        private const double CircumferencePixels = 2 * Math.PI * Radius;
        private const double CircumferenceUnits = CircumferencePixels / StrokeThickness;

        private string _usedAmount = "--";
        private string _totalAmount = "--";
        private string _remainingAmount = "--";
        private string _todayConsumed = "--";
        private string _countdownText = "60s";
        private string _statusText = "正在初始化...";
        private double _targetPercentage = 0;

        private double _percentage = 0;
        private DoubleCollection _dashArray = new DoubleCollection { 0, CircumferenceUnits };
        private bool _isLoading = true;

        public string UsedAmount { get => _usedAmount; set { _usedAmount = value; OnPropertyChanged(); } }
        public string TotalAmount { get => _totalAmount; set { _totalAmount = value; OnPropertyChanged(); } }
        public string RemainingAmount { get => _remainingAmount; set { _remainingAmount = value; OnPropertyChanged(); } }
        public string TodayConsumed { get => _todayConsumed; set { _todayConsumed = value; OnPropertyChanged(); } }
        public string CountdownText { get => _countdownText; set { _countdownText = value; OnPropertyChanged(); } }
        public string StatusText { get => _statusText; set { _statusText = value; OnPropertyChanged(); } }
        public bool IsLoading { get => _isLoading; set { _isLoading = value; OnPropertyChanged(); } }

        public double Percentage
        {
            get => _percentage;
            set
            {
                _percentage = value;
                OnPropertyChanged();
                DashArray = new DoubleCollection { _percentage * CircumferenceUnits, CircumferenceUnits };
            }
        }

        public DoubleCollection DashArray { get => _dashArray; set { _dashArray = value; OnPropertyChanged(); } }

        public MainViewModel()
        {
            LoadHistory();

            // 1秒倒计时定时器
            _countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _countdownTimer.Tick += async (s, e) =>
            {
                _secondsRemaining--;
                CountdownText = $"{_secondsRemaining}s";

                if (_secondsRemaining <= 0)
                {
                    _secondsRemaining = RefreshInterval;
                    CountdownText = $"{_secondsRemaining}s";
                    await UpdateUsageAsync();
                }
            };
            _countdownTimer.Start();

            // 动画定时器
            _animationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _animationTimer.Tick += AnimationTimer_Tick;

            _ = UpdateUsageAsync();
        }

        private void AnimationTimer_Tick(object sender, EventArgs e)
        {
            double diff = _targetPercentage - Percentage;
            if (Math.Abs(diff) < 0.005)
            {
                Percentage = _targetPercentage;
                _animationTimer.Stop();
            }
            else
            {
                Percentage += diff * 0.15;
            }
        }

        public async Task UpdateUsageAsync()
        {
            if (_isUpdating) return;
            _isUpdating = true;
            IsLoading = true;
            StatusText = "正在获取额度...";

            try
            {
                string output = await Task.Run(() => ExecuteCommand("stepcode usage"));

                var match = Regex.Match(output, @"Used\s+[¥$]?\s*([\d.]+).*?Total\s+[¥$]?\s*([\d.]+).*?Remaining\s+[¥$]?\s*([\d.]+)", RegexOptions.Singleline);

                if (match.Success)
                {
                    double used = double.Parse(match.Groups[1].Value);
                    double total = double.Parse(match.Groups[2].Value);
                    double remaining = double.Parse(match.Groups[3].Value);

                    UsedAmount = used.ToString("F2");
                    TotalAmount = total.ToString("F0");
                    RemainingAmount = remaining.ToString("F2");

                    // 【新增】计算今日消耗
                    CalculateTodayConsumed(used);

                    StatusText = $"最后更新: {DateTime.Now:HH:mm:ss}";
                    IsLoading = false;

                    // 重置倒计时（如果在刷新过程中，下一次刷新时间被重置）
                    _secondsRemaining = RefreshInterval;
                    CountdownText = $"{_secondsRemaining}s";

                    _targetPercentage = total > 0 ? (used / total) : 0;
                    _animationTimer.Start();
                }
                else
                {
                    StatusText = "解析失败，输出格式不符";
                    IsLoading = false;
                }
            }
            catch (Exception ex)
            {
                StatusText = $"执行异常: {ex.Message}";
                IsLoading = false;
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private void CalculateTodayConsumed(double currentUsed)
        {
            string today = DateTime.Now.ToString("yyyy-MM-dd");

            if (_usageHistory.TryGetValue(today, out double lastUsedToday))
            {
                // 如果现在的总额小于之前的记录，说明额度被重置了（比如充值或跨周期），今日消耗直接等于当前已用
                if (currentUsed < lastUsedToday)
                {
                    TodayConsumed = currentUsed.ToString("F2");
                }
                else
                {
                    // 今日消耗 = 当前已用 - 今天最早记录下来的已用
                    TodayConsumed = (currentUsed - lastUsedToday).ToString("F2");
                }
            }
            else
            {
                // 今天第一次记录，今日消耗为 0
                TodayConsumed = "0.00";
            }

            // 保存或更新记录
            _usageHistory[today] = currentUsed;
            SaveHistory();
        }

        private void LoadHistory()
        {
            try
            {
                if (File.Exists(_historyFilePath))
                {
                    string json = File.ReadAllText(_historyFilePath);
                    _usageHistory = JsonSerializer.Deserialize<Dictionary<string, double>>(json) ?? new Dictionary<string, double>();
                }
            }
            catch { _usageHistory = new Dictionary<string, double>(); }
        }

        private void SaveHistory()
        {
            try
            {
                string json = JsonSerializer.Serialize(_usageHistory);
                File.WriteAllText(_historyFilePath, json);
            }
            catch { /* 忽略写入异常 */ }
        }

        private string ExecuteCommand(string command)
        {
            try
            {
                var processInfo = new ProcessStartInfo("cmd.exe", "/c " + command)
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8
                };

                using (var process = Process.Start(processInfo))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit(10000);
                    return output;
                }
            }
            catch { return string.Empty; }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}