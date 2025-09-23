using FFmpeg.AutoGen;
using FFMpegLib.Helpers;
using FFMpegLib.Models;
using System.Windows;



namespace FFMpegLib.FFClasses
{
    public unsafe class FFCodec : IDisposable
    {
        public int StreamId { get => StreamInfo.Index; }
        public ffStreamInfo StreamInfo { get; }
        public AVMediaType MediaType { get; private set; } = AVMediaType.AVMEDIA_TYPE_UNKNOWN;
        public AVCodecID CodecID { get; private set; } = AVCodecID.AV_CODEC_ID_FIRST_UNKNOWN;

        public EventHandler<string>? OnError;
        internal AVCodecContext* CodecContext { get => _codecctx; }
        public bool CodecInited => _inited;

        volatile bool _inited = false;

        readonly object _lock = new object();

        AVCodecParameters* _codecpar;
        AVCodec* _codec = null;
        AVCodecContext* _codecctx = null;

        public FFCodec(ffStreamInfo streamInfo)
        {
            StreamInfo = streamInfo;
            InitCodec(streamInfo.CodecParameters);
        }

        void InitCodec(AVCodecParameters* codecpar)
        {
            _codecpar = codecpar;
            MediaType = _codecpar->codec_type;
            CodecID = _codecpar->codec_id;

            if (MediaType != AVMediaType.AVMEDIA_TYPE_AUDIO && MediaType != AVMediaType.AVMEDIA_TYPE_VIDEO) return;
            var codec = ffmpeg.avcodec_find_decoder(_codecpar->codec_id);
            if (codec != null)
            {
                var ctx = ffmpeg.avcodec_alloc_context3(codec);
                if (ctx != null)
                {
                    int err = ffmpeg.avcodec_parameters_to_context(ctx, _codecpar);
                    if (err < 0)
                    {
                        OnError?.Invoke(this, $"codec error {err.av_errorToString()}");
                        ffmpeg.avcodec_free_context(&ctx);
                        return;
                    }
                    err = ffmpeg.avcodec_open2(ctx, codec, null);
                    if (err < 0)
                    {
                        OnError?.Invoke(this, $"codec error {err.av_errorToString()}");
                        ffmpeg.avcodec_free_context(&ctx);
                        return;
                    }
                    lock (_lock)
                    {
                        _codec = codec;
                        _codecctx = ctx;
                        _inited = true;
                    }
                   
                }
                else OnError?.Invoke(this, $"Could not create CodecConext");
            }
            else OnError?.Invoke(this, $"No codec found");
        }

        void Close()
        {
           
            lock (_lock)
            {
                _inited = false;
                if (_codecctx != null)
                {
                    AVCodecContext* ctx = _codecctx;
                    ffmpeg.avcodec_free_context(&ctx);
                    _codecctx = null;
                }
                _codec = null;
            }
        }

        public void Dispose()
        {
            if (_inited) Close();
            GC.SuppressFinalize(this);
        }

    }
}