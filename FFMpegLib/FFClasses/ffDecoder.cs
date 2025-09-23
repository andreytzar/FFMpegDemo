using FFmpeg.AutoGen;
using FFMpegLib.Helpers;
using FFMpegLib.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FFMpegLib.FFClasses
{
    public unsafe class ffDecoder : IDisposable
    {
        readonly BlockingCollection<IntPtr> _packetQueue;
        Task? _decodeTask;

        private CancellationTokenSource? _cts;

        private readonly List<FFCodec> _codecs = new();
        private CancellationTokenSource? _cts;


        public event EventHandler<AVFrame>? OnVideoFrameReady;
        public event EventHandler<AVFrame>? OnAudioFrameReady;
        public event EventHandler<string>? OnError;

        private readonly object _lock = new();

        public ffDecoder(BlockingCollection<IntPtr> packetQueue)
        {
            _packetQueue = packetQueue;
        }

        public void StartDecoding(IEnumerable<ffStreamInfo> streams)
        {
            StopDecoding();

            foreach (var s in streams)
            {
                if (s.MediaType == AVMediaType.AVMEDIA_TYPE_VIDEO ||
                    s.MediaType == AVMediaType.AVMEDIA_TYPE_AUDIO)
                {
                    var codec = new FFCodec(s);
                    if (codec.CodecInited)
                        _codecs.Add(codec);
                }
            }

            _cts = new CancellationTokenSource();
            _decodeTask = Task.Run(() => DecodeLoop(_cts.Token), _cts.Token);
        }



        public void Start()
        {
            Stop();
            if (!_demuxer.IsOpen || _demuxer.PacketQueue == null) return;

            _cts = new CancellationTokenSource();
            _decodeTask = Task.Run(() => DecodeLoop(_cts.Token), _cts.Token);
        }

        private void DecodeLoop(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    if (_demuxer.PacketQueue == null) break;

                    if (!_demuxer.PacketQueue.TryTake(out var ptr, Timeout.Infinite, token))
                        continue;

                    var pkt = (AVPacket*)ptr;
                    if (pkt == null) continue;

                    if (_codecs.TryGetValue(pkt->stream_index, out var codec) && codec.CodecInited)
                    {
                        int ret = ffmpeg.avcodec_send_packet(codec.CodecContext, pkt);
                        if (ret < 0)
                        {
                            OnError?.Invoke(this, $"avcodec_send_packet error {ret.av_errorToString()}");
                            ffmpeg.av_packet_free(&pkt);
                            continue;
                        }
                        AVFrame* frame = ffmpeg.av_frame_alloc();
                        while (true)
                        {
                            ret = ffmpeg.avcodec_receive_frame(codec.CodecContext, frame);
                            if (ret == ffmpeg.AVERROR(ffmpeg.EAGAIN) || ret == ffmpeg.AVERROR_EOF)
                            {
                                break;
                            }
                            if (ret < 0)
                            {
                                OnError?.Invoke(this, $"avcodec_receive_frame error {ret.av_errorToString()}");
                                break;
                            }

                            if (codec.MediaType == AVMediaType.AVMEDIA_TYPE_VIDEO)
                                OnVideoFrameReady?.Invoke(this, *frame);
                            else if (codec.MediaType == AVMediaType.AVMEDIA_TYPE_AUDIO)
                                OnAudioFrameReady?.Invoke(this, *frame);

                            ffmpeg.av_frame_unref(frame);
                        }
                        ffmpeg.av_frame_free(&frame);
                    }

                    ffmpeg.av_packet_free(&pkt);
                }
            }
            catch (OperationCanceledException) { }
        }

        public void Stop()
        {
            _cts?.Cancel();
            try { _decodeTask?.Wait(); } catch { }
            _cts?.Dispose();
            _decodeTask = null;
            _cts = null;
        }

        public void Dispose()
        {
            Stop();
            foreach (var c in _codecs.Values)
                c.Dispose();
            _codecs.Clear();
        }
    }

}
