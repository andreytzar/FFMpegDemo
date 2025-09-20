using FFmpeg.AutoGen;
using FFMpegLib.Helpers;
using NAudio.Gui;
using System.Collections.Concurrent;

using System.Windows.Media.Imaging;


namespace FFMpegLib.FFClasses
{
    public unsafe class FFContext : IDisposable
    {
        public bool IsOpen { get; private set; } = false;
        public EventHandler<string>? OnError;
        public EventHandler<WriteableBitmap>? OnVideoBitmapChange;
        public ConcurrentDictionary<int, FFStream> Streams { get; private set; } = new();

        readonly object _lock = new object();
        AVFormatContext* _avcontext = null;
        AVPacket* pkt = null;
        public bool Open(string path)
        {
            Close();
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
                    IsOpen = true;
                }

                return true;
            }
            catch (Exception e)
            {
                lock (_lock)
                {
                    IsOpen = false;
                }
                OnError?.Invoke(this, e.Message);
            }
            return false;
        }

        public void Play()
        {
            lock (_lock)
            {
                if (!IsOpen) return;
            }
            Task.Run(() =>
            {
                while (pkt != null && ffmpeg.av_read_frame(_avcontext, pkt) >= 0)
                {
                    var ind = pkt->stream_index;
                    if (Streams.TryGetValue(ind, out var stream)) stream.ProceedPacket(pkt);
                    ffmpeg.av_packet_unref(pkt);
                }
            });
        }


        public void Close()
        {
            lock (_lock)
            {
                if (pkt != null)
                {
                    var temp=pkt;
                    ffmpeg.av_packet_free(&temp);
                    pkt = null;
                }
                foreach (var strem in Streams)
                {
                    strem.Value.OnVideoBitmapChange -= OnVideoBitmapChange;
                    strem.Value.Dispose();
                }
                Streams.Clear();
                if (_avcontext != null)
                {
                    var avc = _avcontext;
                    ffmpeg.avformat_close_input(&avc);
                    _avcontext = null;
                }
                IsOpen = false;
            }
        }

        public void Dispose()
        {
            Close();
            GC.SuppressFinalize(this);
        }
    }
}
