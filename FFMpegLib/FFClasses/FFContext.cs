using FFmpeg.AutoGen;
using FFMpegLib.Helpers;
using NAudio.Gui;
using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Media.Imaging;


namespace FFMpegLib.FFClasses
{
    public unsafe class FFContext : IDisposable
    {
        public  bool IsOpened { get => IsOpen; }
       
        public EventHandler<string>? OnError;
        public EventHandler<WriteableBitmap>? OnVideoBitmapChange;
        public ConcurrentDictionary<int, FFStream> Streams { get; private set; } = new();

        volatile bool IsOpen = false;
        readonly object _lock = new object();

        AVFormatContext* _avcontext = null;
        AVPacket* pkt = null;

        public bool Open(string path)
        {
            try
            {
                AVPacket* p = ffmpeg.av_packet_alloc();
                if (p!=null) pkt = p;
                AVFormatContext* fmt = null;
                int err = ffmpeg.avformat_open_input(&fmt, path, null, null);
                if (err != 0)
                    throw new Exception(err.av_errorToString());
                err = ffmpeg.avformat_find_stream_info(fmt, null);
                if (err < 0)
                {
                    ffmpeg.avformat_close_input(&fmt);
                    throw new Exception(err.av_errorToString());
                }
                for (int i = 0; i < fmt->nb_streams; i++)
                {
                    var stream = fmt->streams[i];
                    FFStream ffs = new(stream);
                    ffs.OnVideoBitmapChange += OnVideoBitmapChange;
                    Streams.TryAdd(i, ffs);
                }

                lock (_lock)
                {
                    _avcontext = fmt;
                    IsOpen=true;
                }

                return true;
            }
            catch (Exception e)
            {                   
                IsOpen = false;
                OnError?.Invoke(this, e.Message);
            }
            return IsOpen;
        }

        public void Play()
        {
            if (!IsOpen) return;
            Task.Run(() =>
            {
                while (IsOpen && pkt != null && ffmpeg.av_read_frame(_avcontext, pkt) >= 0)
                {
                    var ind = pkt->stream_index;
                    if (Streams.TryGetValue(ind, out var stream)) stream?.ProceedPacket(pkt);
                    ffmpeg.av_packet_unref(pkt);
                }
            });
        }

        
        public void Close()
        {
            IsOpen = false;
            lock (_lock)
            {
                foreach (var strem in Streams)
                {
                    strem.Value.OnVideoBitmapChange -= OnVideoBitmapChange;
                    strem.Value.Dispose();
                }

                Streams.Clear();

                if (pkt != null)
                {
                    var temp=pkt;
                    ffmpeg.av_packet_free(&temp);
                    pkt = null;
                }

                if (_avcontext != null)
                {
                    var avc = _avcontext;
                    ffmpeg.avformat_close_input(&avc);
                    _avcontext = null;
                }
            }
        }

        public void Dispose()
        {
            Close();
        }
    }
}
