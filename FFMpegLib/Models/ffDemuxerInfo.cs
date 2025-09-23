using FFmpeg.AutoGen;
using System;
using System.Collections.Generic;
using System.IO.Packaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace FFMpegLib.Models
{
    public class ffDemuxerInfo
    {
        public string FilePath { get; internal set; }=string.Empty;
        public string FormatName { get; internal set; } = string.Empty;
        public string LongFormatName { get; internal set; } = string.Empty;
        public long DurationMs { get; internal set; }
        public long BitRate { get; internal set; }
        public int StreamCount { get; internal set; }
        public string Metadata { get; internal set; } = string.Empty;

        public List<ffStreamInfo> Streams { get; internal set; } = new();

        public override string ToString()
        {
            string text = $"Format: {FormatName} LongName: {LongFormatName} Duration: {TimeSpan.FromMilliseconds(DurationMs)} BitRate: {BitRate} Streams: {StreamCount}\r\nMetadata: {Metadata}";
            foreach (var s in Streams)
                text = $"{text}\r\n{s}";
            return text ;
        }
    }
    internal static unsafe class ffDemuxerInfoExtractor
    {
        internal static ffDemuxerInfo? FromFormatContext(AVFormatContext* fmt, string? path = null)
        {
            try
            {
                var info = new ffDemuxerInfo
                {
                    FilePath = path ?? "",
                    FormatName = Marshal.PtrToStringAnsi((IntPtr)fmt->iformat->name) ?? "",
                    LongFormatName = Marshal.PtrToStringAnsi((IntPtr)fmt->iformat->long_name) ?? "",
                    DurationMs = fmt->duration > 0 ? fmt->duration / (ffmpeg.AV_TIME_BASE / 1000) : 0,
                    BitRate = fmt->bit_rate,
                    StreamCount = (int)fmt->nb_streams,
                    Metadata = ExtractMetadata(fmt->metadata) ?? ""
                };

                for (int i = 0; i < fmt->nb_streams; i++)
                {
                    var st = fmt->streams[i];
                    var cp = st->codecpar;

                    var si = new ffStreamInfo
                    {
                        Index = st->index,
                        MediaType = cp->codec_type,
                        CodecId = cp->codec_id,
                        CodecName = ffmpeg.avcodec_get_name(cp->codec_id),
                        BitRate = cp->bit_rate,
                        Duration = st->duration,
                        StartTime = st->start_time,
                        TimeBase = st->time_base,
                        SampleAspectRatio = cp->sample_aspect_ratio,
                        CodecParameters=cp,
                    };

                    if (cp->codec_type == AVMediaType.AVMEDIA_TYPE_VIDEO)
                    {
                        si.Width = cp->width;
                        si.Height = cp->height;
                        si.FrameRate = st->avg_frame_rate;
                    }
                    else if (cp->codec_type == AVMediaType.AVMEDIA_TYPE_AUDIO)
                    {
                        si.SampleRate = cp->sample_rate;
                        si.Channels = cp->ch_layout.nb_channels;
                        si.ChannelLayout = cp->ch_layout;
                        si.SampleFormat = (AVSampleFormat)cp->format;
                    }

                    info.Streams.Add(si);
                }
                return info;
            }
            catch { }
            return null;
        }

        private static string? ExtractMetadata(AVDictionary* dict)
        {
            if (dict == null) return null;
            var list = new List<string>();
            AVDictionaryEntry* tag = null;
            while ((tag = ffmpeg.av_dict_get(dict, "", tag, ffmpeg.AV_DICT_IGNORE_SUFFIX)) != null)
            {
                string key = Marshal.PtrToStringAnsi((IntPtr)tag->key) ?? "";
                string value = Marshal.PtrToStringAnsi((IntPtr)tag->value) ?? "";
                list.Add($"{key}={value}");
            }
            return string.Join("; ", list);
        }
    }
}
