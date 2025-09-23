using FFMpegLib.FFClasses;
using FFMpegLib.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace FFMpegLib
{
    public class Player:IDisposable
    {
        public ffDemuxerInfo? DemuxerInfo { get => _demuxer.DemuxerInfo; }

        public event EventHandler<string>? OnError { add { _demuxer.OnError += value; } remove { _demuxer.OnError -= value; } }
        public event EventHandler<string>? OnInfo { add { _demuxer.OnInfo += value; } remove { _demuxer.OnInfo -= value; } }
        public event EventHandler<WriteableBitmap>? OnVideoBitmapChange;
        ffDemuxer _demuxer = new();
        Ffmpg ffmpg=Ffmpg.Instance;

       // public void Play() => _demuxer?.Play();
        public bool OpenFile(string file)
        {
            string _file = file.Trim();
            if (!string.IsNullOrEmpty(_file) && File.Exists(_file))
                return _demuxer.OpenFile(_file)==true;
            return false;
        }

        public void Close() => _demuxer.Close();

        public void Dispose()
        {
            _demuxer.Dispose();
        }
    }
}
