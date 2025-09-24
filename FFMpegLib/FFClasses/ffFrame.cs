using FFmpeg.AutoGen;


namespace FFMpegLib.FFClasses
{
    public unsafe class ffFrame : IDisposable
    {
        public AVMediaType MediaType { get; private set; }
        internal AVFrame* frame { get; private set; }
        internal AVRational TimeBase { get; private set; }
        internal long StartYime { get; private set; }
        public ffFrame(AVMediaType mediaType, AVFrame* frame, AVRational timeBase, long startYime)
        {
            MediaType = mediaType;
            this.frame = frame;
            TimeBase = timeBase;
            StartYime = startYime;
        }

        public void Dispose()
        {
            if (frame != null)
            {
                var temp = frame;
                ffmpeg.av_frame_free(&temp);
                frame = null;
            }
        }
    }
}
