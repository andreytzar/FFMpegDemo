using FFMpegLib;
using FFMpegLib.Helpers.VM;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;

namespace FFMpegDemo.Pages
{
    /// <summary>
    /// Логика взаимодействия для PagePlayer.xaml
    /// </summary>
    public partial class PagePlayer : Page, IDisposable
    {
        public event EventHandler<string> StatusChanged { add { vm.StatusChanged += value; } remove { vm.StatusChanged -= value; } }
        VMPagePlayer vm = new();
        public PagePlayer(EventHandler<string> statusChanged)
        {
            InitializeComponent();
            DataContext = vm;
            StatusChanged += statusChanged;
        }
        public void Dispose()
        {
            vm.Dispose();
        }
    }

    public class VMPagePlayer : VMNotifyPropretyChanged, IDisposable
    {
        string _file = string.Empty;
        public string file { get => _file; set { if (_file != value) { _file = value; _file = _file.Trim(); OnPropertyChanged(); OnFileChanged(); } } }
        string _Error = string.Empty;
        public string Error { get => _Error; set { { _Error = value; OnPropertyChanged(); Task.Run(async () => {await Task.Delay(25000); Error = ""; }); } } }
        WriteableBitmap? _VideoBitmap;
        public WriteableBitmap? VideoBitmap { get => _VideoBitmap; set { if (_VideoBitmap != value) { _VideoBitmap = value; OnPropertyChanged(); } } }
        public event EventHandler<string>? StatusChanged;

        public BCommand BCOpenFile { get; }
        FFPlayer _player = new();

        public VMPagePlayer()
        {
            _player.OnError += _player_OnError;
            _player.OnVideoBitmapChange += _player_OnVideoBitmapChange;
            BCOpenFile = new((o) =>
            {
                file = string.Empty;
                var diag = new OpenFileDialog()
                {
                    Title = "Відкрити відео файл",
                    Filter = "Відео файли|*.mp4;*.mkv;*.avi;*.mov;*.flv;*.wmv;*.webm;*.ts;*.mpeg;*.mpg;*.3gp|Усі файли|*.*",
                    CheckFileExists = true,
                    CheckPathExists = true,
                    Multiselect = false
                };
                if (diag.ShowDialog() == true)
                    file = diag.FileName;
            });
        }

        private void _player_OnVideoBitmapChange(object? sender, WriteableBitmap e)
        {
            VideoBitmap = e;
        }

        private void _player_OnError(object? sender, string e) => Error = e;


        void OnFileChanged()
        {
            if (!string.IsNullOrEmpty(file) && File.Exists(file))
                Task.Run(() =>
                {
                    if (_player.OpenFile(file))
                    {
                        _player.Play();
                        string mess = $"Playing {file}. Found streams:";
                        foreach (var strem in _player.Streams)
                            mess = $"{mess}\r\n{strem}";
                        Error = mess;
                    }
                });
        }

        public void Dispose()
        {
            _player.Dispose();

        }
    }
}
