using FFmpeg.AutoGen;
using FFMpegLib.FFClasses;


namespace FFMpegLib.Models
{
    public unsafe class ffStreamInfo
    {
        public int Index { get; internal set; }

        internal AVCodecParameters* CodecParameters;

        public AVMediaType MediaType { get; internal set; }
        public AVCodecID CodecId { get; internal set; }
        public string? CodecName { get; internal set; }
        public long BitRate { get; internal set; }
        public long Duration { get; internal set; }
        public long StartTime { get; internal set; }

        // Для відео
        public int Width { get; internal set; }
        public int Height { get; internal set; }
        public AVRational TimeBase { get; internal set; }
        public AVRational FrameRate { get; internal set; }
        public AVRational SampleAspectRatio { get; internal set; }

        // Для аудіо
        public int SampleRate { get; internal set; }
        public int Channels { get; internal set; }
        public AVChannelLayout ChannelLayout { get; internal set; }
        public AVSampleFormat SampleFormat { get; internal set; }


        public override string ToString()
        {
            string text = $"\t{Index}: {MediaType} {CodecId} {CodecName} BitRate:{BitRate} Duration:{Duration} StartTime:{StartTime}";
            switch (MediaType)
            {
                case AVMediaType.AVMEDIA_TYPE_VIDEO:
                    text = $"{text}\r\n\tVideo {Width}x{Height} TimeBase {TimeBase.num}:{TimeBase.den} FrameRate {FrameRate.num}:{FrameRate.den} Ratio {SampleAspectRatio.num}:{SampleAspectRatio.den}";
                    break;
                case AVMediaType.AVMEDIA_TYPE_AUDIO:
                    text = $"{text}\r\n\tAudio SampleRate:{SampleRate} Channels:{Channels} SampleFormat {SampleFormat}";
                    break;
            }
            return text ;
        }
    }
}
