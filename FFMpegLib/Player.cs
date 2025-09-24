using FFMpegLib.FFClasses;
using FFMpegLib.Models;
using System.IO;
using System.Windows.Media.Imaging;


namespace FFMpegLib
{
    public class Player:IDisposable
    {
        public ffDemuxerInfo? DemuxerInfo { get => _demuxer.DemuxerInfo; }

        public event EventHandler<string>? OnError { add { _demuxer.OnError += value; _decoder.OnError += value; }
            remove { _demuxer.OnError -= value; _decoder.OnError -= value; } }
        public event EventHandler<string>? OnInfo { add { _demuxer.OnInfo += value; _decoder.OnInfo += value; } 
            remove { _demuxer.OnInfo -= value; _decoder.OnInfo -= value; } }

        public event EventHandler<WriteableBitmap>? OnVideoChange
        {
            add { _render.OnVideoChange += value; }
            remove { _render.OnVideoChange -= value; }
        }
        ffRender _render;
        ffDecoder _decoder;
        ffDemuxer _demuxer = new();

        public Player()
        {
            _= Ffmpg.Instance;
            _decoder = new(_demuxer.PacketQueue);
            _render = new(_decoder.FrameQueue);
        }
        public bool OpenFile(string file)
        {
            string _file = file.Trim();
            Close();
            if (!string.IsNullOrEmpty(_file) && File.Exists(_file))
            {
                var res= _demuxer.OpenFile(_file) == true;
                if (res && _demuxer.DemuxerInfo!=null && _demuxer.DemuxerInfo.Streams.Count>0)
                {
                    _demuxer.StartReading();
                    _decoder.StartDecoding(_demuxer.DemuxerInfo.Streams);
                    _render.StartRender();
                }
                return res;
            }
            return false;
        }



        public void Close()
        {
            _demuxer.Close();
            _decoder.StopDecoding();
            _render.StopRender();
        }

        public void Dispose()
        {
            Close();
            _render.Dispose();
            _decoder.Dispose();
            _demuxer.Dispose();
        }
    }
}
