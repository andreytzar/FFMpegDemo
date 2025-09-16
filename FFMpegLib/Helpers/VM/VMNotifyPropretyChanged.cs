using System.ComponentModel;
using System.Runtime.CompilerServices;


namespace FFMpegLib.Helpers.VM
{
    public class VMNotifyPropretyChanged : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string propertyName = "")
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
