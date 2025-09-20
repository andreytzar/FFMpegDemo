using FFmpeg.AutoGen;
using NAudio.Gui;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FFMpegLib.FFClasses
{
    public unsafe class FFStream : IDisposable
    {
        public EventHandler<WriteableBitmap>? OnVideoBitmapChange;
        public int Index { get; private set; } = -1;
        public long Duration { get; private set; } = 0;
        public long StartTime { get; private set; } = 0;

        public FFCodec? Codec { get; private set; }
        public StreamType StreamType { get; private set; } = StreamType.UNKNOWN;
        public CodecID CodecID { get; private set; } = CodecID.UNKNOWN;
        readonly object _lock = new object();

        AVStream* _stream = null;
        SwsContext* _swsctx = null;
        AVFrame* _frame = null;
        AVFrame* _rgbframe = null;
        WriteableBitmap? _bitmap;

        volatile bool _disposed = false;

        public FFStream(AVStream* stream)
        {
            _stream = stream;
            Index = stream->index;
            Duration = stream->duration;
            StartTime = stream->start_time;
            Codec = new(stream->codecpar);
            StreamType = (StreamType)_stream->codecpar->codec_type;
            CodecID = (CodecID)_stream->codecpar->codec_id;
            _frame = ffmpeg.av_frame_alloc();
        }

        void InitSWS()
        {
            if (StreamType != StreamType.VIDEO || Codec == null || !Codec.CodecInited || Codec.VideoParam == null || Codec.CodecContext == null) return;
            Free();
            var param = Codec.VideoParam;
            var sws = ffmpeg.sws_getContext(param.Width, param.Height, param.PixelFormat,
                     param.Width, param.Height, AVPixelFormat.AV_PIX_FMT_BGR24, ffmpeg.SWS_BILINEAR, null, null, null);
            if (sws != null)
            {
                _swsctx = sws;

                var rgbFrame = ffmpeg.av_frame_alloc();
                rgbFrame->format = (int)AVPixelFormat.AV_PIX_FMT_BGR24;
                rgbFrame->width = param.Width;
                rgbFrame->height = param.Height;
                if (ffmpeg.av_frame_get_buffer(rgbFrame, 1) >= 0)
                {
                    _rgbframe = rgbFrame;
                    Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        _bitmap = new WriteableBitmap(param.Width, param.Height, 96, 96, PixelFormats.Bgr24, null);
                        OnVideoBitmapChange?.Invoke(this, _bitmap);
                    });
                }
            }

        }


        internal void ProceedPacket(AVPacket* pkt)
        {
            if (pkt->stream_index != Index) return;
            switch (StreamType)
            {
                case StreamType.VIDEO:
                    ProceedVideoPacket(pkt);
                    break;

            }
        }

        void ProceedVideoPacket(AVPacket* pkt)
        {
            if (_disposed) return;
            bool needInit = false;
            lock (_lock)
            {
                if (_disposed) return;
                if (Codec == null || !Codec.CodecInited || Codec.CodecContext == null || Codec.VideoParam == null) return;
                if (_frame == null)
                    _frame = ffmpeg.av_frame_alloc();
                if (_rgbframe == null || _swsctx == null || _bitmap == null) needInit = true;
            }

            if (needInit) InitSWS();
            int err = 0;
            try
            {
                if (_disposed || _swsctx == null || _bitmap == null || Codec.CodecContext == null || pkt == null) return;
                err = ffmpeg.avcodec_send_packet(Codec.CodecContext, pkt);
            }
            catch { return; }
            if (err < 0 || _disposed) return;

            if (err >= 0)
            {
                while (!_disposed && ffmpeg.avcodec_receive_frame(Codec.CodecContext, _frame) == 0)
                {
                    if (_rgbframe == null || _rgbframe->width != _frame->width || _rgbframe->height != _frame->height)
                    {
                        InitSWS();
                        if (_disposed || _swsctx == null || _rgbframe == null || _bitmap == null) break;
                    }

                    ffmpeg.sws_scale(_swsctx, _frame->data, _frame->linesize, 0, Codec.VideoParam.Height,
                        _rgbframe->data, _rgbframe->linesize);

                    int stride = _rgbframe->linesize[0];
                    int bufSize = stride * Codec.VideoParam.Height;

                    if (_disposed) break;

                    if (_bitmap != null)
                    {
                        Application.Current.Dispatcher.BeginInvoke(() =>
                        {
                            if (_disposed) return;
                            _bitmap.WritePixels(new Int32Rect(0, 0, Codec.VideoParam.Width, Codec.VideoParam.Height),
                             (IntPtr)_rgbframe->data[0], bufSize, stride);
                        });

                    }
                }
            }
            if (_disposed) Free();
        }


        void Free()
        {
            lock (_lock)
            {
                if (_rgbframe != null)
                {
                    var temp = _rgbframe;
                    ffmpeg.av_frame_free(&temp);
                    _rgbframe = null;
                }
                if (_swsctx != null)
                {
                    var temp = _swsctx;
                    ffmpeg.sws_freeContext(temp);
                    _swsctx = null;
                }
                _bitmap = null;
            }
            if (_disposed)
            {
                Codec?.Dispose();
                if (_frame != null)
                {
                    var temp = _frame;
                    ffmpeg.av_frame_free(&temp);
                    _frame = null;
                }

            }
        }

        public void Dispose()
        {
            _disposed = true;
        }

        public override string ToString()
        {
            return $"strem {Index} {Codec}";
        }
    }

    public enum StreamType : int
    {
        /// <summary>Usually treated as AVMEDIA_TYPE_DATA</summary>
        UNKNOWN = -1,
        VIDEO = 0,
        AUDIO = 1,
        /// <summary>Opaque data information usually continuous</summary>
        DATA = 2,
        SUBTITLE = 3,
        /// <summary>Opaque data information usually sparse</summary>
        ATTACHMENT = 4,
        TYPE_NB = 5,
    }
}

