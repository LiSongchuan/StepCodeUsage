using System;
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
    // 记录每日基准值的模型
    public class DailyUsageRecord
    {
        public string Date { get; set; }
        public double BaseUsedAmount { get; set; } // 当天第一次启动时的已用额度，作为基准
    }

    public class MainViewModel : INotifyPropertyChanged
    {
        private DispatcherTimer _countdownTimer;
        private DispatcherTimer _animationTimer;
        private bool _isUpdating = false;

        private const int RefreshInterval = 60;
        private int _secondsRemaining = RefreshInterval;

        // 本地历史记录文件路径
        private readonly string _historyFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "usage_history.json");
        private DailyUsageRecord _todayRecord;

        // 圆环参数 (请确保与 XAML 里的 Ellipse Width 严格对应，260/2 = 130)
        private const double Radius = 130;
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
                // 视觉底线设为 1%，防止低占比时圆环完全不可见
                double visualPercentage = Math.Max(_percentage, 0.01);
                DashArray = new DoubleCollection { visualPercentage * CircumferenceUnits, CircumferenceUnits };
            }
        }

        public DoubleCollection DashArray { get => _dashArray; set { _dashArray = value; OnPropertyChanged(); } }

        public MainViewModel()
        {
            LoadHistory();

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

                    // ============ 全新的今日消耗计算逻辑 ============
                    string today = DateTime.Now.ToString("yyyy-MM-dd");

                    // 1. 如果记录不存在，或者跨天了
                    if (_todayRecord == null || _todayRecord.Date != today)
                    {
                        // 跨天：将当前已用额度作为新一天的基准，并保存
                        _todayRecord = new DailyUsageRecord
                        {
                            Date = today,
                            BaseUsedAmount = used
                        };
                        SaveHistory();
                        TodayConsumed = "0.00";
                    }
                    else
                    {
                        // 同一天：检查是否因为充值/重置导致额度倒退
                        if (used < _todayRecord.BaseUsedAmount)
                        {
                            // 额度被重置了，今天的基准必须重新算（否则今日消耗会出现负数）
                            _todayRecord.BaseUsedAmount = used;
                            SaveHistory();
                            TodayConsumed = "0.00";
                        }
                        else
                        {
                            // 正常情况：当前已用 - 当天基准
                            double consumedToday = used - _todayRecord.BaseUsedAmount;
                            TodayConsumed = consumedToday.ToString("F2");
                        }
                    }
                    // ==============================================

                    StatusText = $"最后更新: {DateTime.Now:HH:mm:ss}";
                    IsLoading = false;

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

        private void LoadHistory()
        {
            try
            {
                if (File.Exists(_historyFilePath))
                {
                    string json = File.ReadAllText(_historyFilePath);
                    _todayRecord = JsonSerializer.Deserialize<DailyUsageRecord>(json);
                }
            }
            catch { _todayRecord = null; }
        }

        private void SaveHistory()
        {
            try
            {
                if (_todayRecord != null)
                {
                    string json = JsonSerializer.Serialize(_todayRecord);
                    File.WriteAllText(_historyFilePath, json);
                }
            }
            catch { /* 忽略异常 */ }
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