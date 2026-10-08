using System.ComponentModel;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace StepCodeUsage
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainViewModel ViewModel => (MainViewModel)this.DataContext;

        public MainWindow()
        {
            InitializeComponent();
            this.Closing += MainWindow_Closing;
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            e.Cancel = true; // 取消关闭
            this.Hide();     // 隐藏到托盘
            // 注意：去掉了非必要的 GC.Collect，防止界面卡顿
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is MainViewModel viewModel)
            {
                await viewModel.UpdateUsageAsync();
            }
        }

        private void Hide_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
        }
    }
}