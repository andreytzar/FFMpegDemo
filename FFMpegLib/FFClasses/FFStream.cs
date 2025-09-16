using FFmpeg.AutoGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace FFMpegLib.FFClasses
{
    public unsafe class FFStream:IDisposable
    {
        public int Index { get; private set; } = -1;
        public long Duration { get; private set; } = 0;
        public long StartTime { get; private set; } = 0;

        public FFCodec? Codec { get; private set; }
        public StreamType StreamType { get; private set; }= StreamType.UNKNOWN;
        public CodecID CodecID { get; private set; } = CodecID.UNKNOWN;
        object _lock=new object();
        
        AVStream* _stream=null;
        SwsContext* _swsctx=null;
        AVFrame* _frame = null;
        AVFrame* _rgbfame = null;

        public FFStream(AVStream* stream) 
        { 
            _stream = stream;
            Index=stream->index;
            Duration = stream->duration;
            StartTime=stream->start_time;
            Codec = new(stream->codecpar);
            StreamType =(StreamType)_stream->codecpar->codec_type;
            CodecID = (CodecID)_stream->codecpar->codec_id;
        }

        void InitSWS()
        {
            if (StreamType != StreamType.VIDEO || Codec==null || !Codec.CodecInited || Codec.VideoParam==null|| Codec.CodecContext==null) return;
            var param=Codec.VideoParam;
            var sws = ffmpeg.sws_getContext(param.Width, param.Height, param.PixelFormat,
                     param.Width, param.Height, AVPixelFormat.AV_PIX_FMT_BGR24, ffmpeg.SWS_BILINEAR, null, null, null);
            if (sws != null)
                _swsctx = sws;
        }

        public void ProceedPacket(AVPacket* pkt)
        {
            if (pkt->stream_index!=Index) return;
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
                if (Codec == null || !Codec.CodecInited || Codec.CodecContext == null || Codec.VideoParam==null) return;
                if (_frame==null) _frame=ffmpeg.av_frame_alloc();
                if (_rgbfame == null)
                {
                    _rgbfame = ffmpeg.av_frame_alloc();
                    int rgbBufSize = ffmpeg.av_image_get_buffer_size(AVPixelFormat.AV_PIX_FMT_BGR24, Codec.VideoParam.Width, Codec.VideoParam.Height, 1);
                    byte* rgbBuf = (byte*)ffmpeg.av_malloc((ulong)rgbBufSize);
                    ffmpeg.av_image_fill_arrays(ref _rgbfame->data0, ref _rgbfame->linesize0, rgbBuf,
                        AVPixelFormat.AV_PIX_FMT_BGR24, vCtx->width, vCtx->height, 1);
                }
                if (_swsctx==null) InitSWS();
                if (_swsctx == null) return ;
            }
           int err=ffmpeg.avcodec_send_packet(Codec.CodecContext, pkt);
            if (err >= 0)
            {
                while (ffmpeg.avcodec_receive_frame(Codec.CodecContext, _frame) == 0)
                {
                    ffmpeg.sws_scale(_swsctx, _frame->data, _frame->linesize, 0, Codec.VideoParam.Height,
                        rgbFrame->data, rgbFrame->linesize);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        _bitmap!.WritePixels(new Int32Rect(0, 0, vCtx->width, vCtx->height),
                            (IntPtr)rgbFrame->data[0], rgbBufSize, rgbFrame->linesize[0]);
                        FrameReady?.Invoke(_bitmap);
                    });

                    Thread.Sleep(40);
                }
            }

        }


        void prepareVideoPlay()
        {

        }

        void FreeVide()
        {
            lock (_lock)
            {
                if (_swsctx != null)
                {
                    var temp = _swsctx;
                    ffmpeg.sws_freeContext(temp);
                    _swsctx = null;
                }
            }
        }
        void Free()
        {
            lock (_lock)
            {
                if (_frame != null)
                {
                    var temp = _frame;
                    ffmpeg.av_frame_free(temp);
                    _frame = null;
                }
            }
        }
        public void Dispose()
        {
            FreeVide();
            Codec?.Dispose();
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

