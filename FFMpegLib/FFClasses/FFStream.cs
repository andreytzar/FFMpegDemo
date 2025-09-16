using FFmpeg.AutoGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        AVStream* _stream;
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

        public void Dispose()
        {
            throw new NotImplementedException();
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

