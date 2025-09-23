using FFMpegDemo.ViewModels;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace FFMpegDemo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        VMMain vm;
        public MainWindow()
        {
            InitializeComponent();
            DataContext = vm = new(OnActivateChanged);
            this.Closing += MainWindow_Closing;
        }
        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            vm.Dispose();
        }
        private void NavBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
                if (btn.DataContext is VMNavBtn navbtn)
                    OnActivateChanged(vm, navbtn);
        }
        void OnActivateChanged(object? sender, VMNavBtn navbtn)
        {
            if (sender is VMMain vM)
                Application.Current.Dispatcher.Invoke(() =>
                {
                    frame.Navigate(navbtn.Page);
                    foreach (var nav in vM.NavBtns)
                        nav.IsActive = false;
                    navbtn.IsActive = true;
                });
        }
    }
}