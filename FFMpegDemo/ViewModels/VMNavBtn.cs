
using FFMpegLib.Helpers.VM;
using System.Windows.Controls;

namespace FFMpegDemo.ViewModels
{
    public class VMNavBtn: VMNotifyPropretyChanged
    {
        public Page Page { get; set; }
        
        bool _IsActive=false;
        public bool IsActive { get=>_IsActive; set { if (_IsActive != value) { _IsActive = value; OnPropertyChanged(); } } }
        
        string _MenuText = string.Empty;
        public string MenuText { get => _MenuText; set { if (_MenuText != value) { _MenuText = value; OnPropertyChanged(); } } }

    }
}
