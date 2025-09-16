using FFmpeg.AutoGen;
using System.Windows.Media.Imaging;
using System.Windows;              // для Int32Rect
using System.Windows.Media;

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
        object _lock = new object();

        AVStream* _stream = null;
        SwsContext* _swsctx = null;
        AVFrame* _frame = null;
        AVFrame* _rgbframe = null;
        WriteableBitmap? _bitmap;

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
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        _bitmap = new WriteableBitmap(param.Width, param.Height, 96, 96, PixelFormats.Bgr24, null);
                        OnVideoBitmapChange?.Invoke(this, _bitmap);
                    });
                }
            }
        }

        public void ProceedPacket(AVPacket* pkt)
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
            lock (_lock)
            {
                if (Codec == null || !Codec.CodecInited || Codec.CodecContext == null || Codec.VideoParam == null) return;
                if (_frame == null)
                    _frame = ffmpeg.av_frame_alloc();
                if (_rgbframe->width != _frame->width || _rgbframe->height != _frame->height)
                    InitSWS();
                if (_swsctx == null) InitSWS();
                if (_swsctx == null || _bitmap == null) return;
            }
            int err = ffmpeg.avcodec_send_packet(Codec.CodecContext, pkt);
            if (err >= 0)
            {
                while (ffmpeg.avcodec_receive_frame(Codec.CodecContext, _frame) == 0)
                {
                    ffmpeg.sws_scale(_swsctx, _frame->data, _frame->linesize, 0, Codec.VideoParam.Height,
                        _rgbframe->data, _rgbframe->linesize);

                    int stride = _rgbframe->linesize[0];
                    int bufSize = stride * Codec.VideoParam.Height;
                    WriteableBitmap? bmp;
                    lock (_lock) bmp = _bitmap;

                    if (bmp != null)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            bmp.WritePixels(new Int32Rect(0, 0, Codec.VideoParam.Width, Codec.VideoParam.Height),
                                 (IntPtr)_rgbframe->data[0], bufSize, stride);
                        });
                        OnVideoBitmapChange?.Invoke(this, bmp);
                    }
                }
            }
        }


        void prepareVideoPlay()
        {

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
        }

        public void Dispose()
        {
            Free();
            lock (_lock)
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

