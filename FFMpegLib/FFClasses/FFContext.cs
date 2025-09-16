using FFmpeg.AutoGen;
using FFMpegLib.Helpers;
using System.Collections.Concurrent;


namespace FFMpegLib.FFClasses
{
    public unsafe class FFContext : IDisposable
    {
        public bool IsOpen { get; private set; } =false;
        public EventHandler<string>? OnError;
        public ConcurrentDictionary<int, FFStream> Streams { get; private set; }=new ();

        readonly object _lock = new object();
        AVFormatContext* _avcontext = null;
        public bool Open(string path)
        {
            Close();
            try
            {
                AVFormatContext* fmt = null;
                int err = ffmpeg.avformat_open_input(&fmt, path, null, null);
                if (err != 0)
                    throw new Exception(err.av_errorToString());
                err = ffmpeg.avformat_find_stream_info(fmt, null);
                if (err < 0)
                    throw new Exception(err.av_errorToString());
                for (int i = 0; i < fmt->nb_streams; i++)
                {
                    var sream = fmt->streams[i];
                    lock (_lock)
                    {
                        Streams.TryAdd(i,new(sream));
                    }
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

        public void Close()
        {
            lock (_lock)
            {
                foreach (var strem in Streams)
                    strem.Value.Dispose();
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
