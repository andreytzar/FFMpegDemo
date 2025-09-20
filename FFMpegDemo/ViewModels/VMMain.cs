
using FFMpegDemo.Pages;
using FFMpegDemo.ViewModels;
using FFMpegLib.Helpers.VM;
using System.Collections.ObjectModel;


namespace FFMpegDemo.ViewModels
{
    public class VMMain : VMNotifyPropretyChanged
    {

        string _Status = string.Empty;
        public string Status { get => _Status; set { if (_Status != value) { _Status = value; OnPropertyChanged(); } } }

        public ObservableCollection<VMNavBtn> NavBtns { get; private set; } = new();

        public EventHandler<VMNavBtn>? ActivatePage;


        public VMMain(EventHandler<VMNavBtn>? activatePage) {
            ActivatePage += activatePage;
            InitNavBtns();
        }
        
        void InitNavBtns()
        {
            NavBtns.Clear();

            var b = new VMNavBtn() { Page = new PagePlayer(OnStatusChanged), MenuText = "PLayer" };
            NavBtns.Add(b);
            //NavBtns.Add(new VMNavBtn() { Page = new PageOnVif(OnStatusChanged), MenuText = "OnVif" });

            ActivatePage?.Invoke(this, b);
        }

        private void OnStatusChanged(object? sender, string e) => Status = e;

    }
}
