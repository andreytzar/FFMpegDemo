using FFmpeg.AutoGen;
using FFMpegLib.Models;
using System.Collections.Concurrent;


namespace FFMpegLib.FFClasses
{
    public unsafe class ffDecoder : IDisposable
    {
        static int _frameQueueCount = 50;
        public BlockingCollection<ffFrame> FrameQueue { get; private set; } = new ();
        public event EventHandler<string>? OnError;
        public event EventHandler<string>? OnInfo;

        readonly BlockingCollection<IntPtr> _packetQueue;

        Dictionary<int, FFCodec> _codecs = new();

        Task? _decodeTask;
        CancellationTokenSource? _ctsdecoder;
        readonly object _lock = new();

        public ffDecoder(BlockingCollection<IntPtr> packetQueue)
        {
            _packetQueue = packetQueue;
        }

        public void StartDecoding(IEnumerable<ffStreamInfo> streams)
        {
            StopDecoding();
            ClearCodecs();

            foreach (var s in streams)
            {
                if (s.MediaType == AVMediaType.AVMEDIA_TYPE_VIDEO ||
                    s.MediaType == AVMediaType.AVMEDIA_TYPE_AUDIO)
                {
                    var codec = new FFCodec(s);
                    codec.OnError += OnError;
                    if (codec.CodecInited)
                        _codecs.TryAdd(s.Index, codec);
                }
            }
            if (_codecs.Count > 0)
            {
                OnInfo?.Invoke(this, $"Decoder: Start Decoding. Codecs to Decoding: {_codecs.Count}");
                _ctsdecoder = new CancellationTokenSource();
                _decodeTask = Task.Run(() => DecodeLoop(_ctsdecoder.Token), _ctsdecoder.Token);
            }
            else OnError?.Invoke(this, $"Decoder: No codecs avalible");

        }


        private void DecodeLoop(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    if (_packetQueue == null) break;
                    if (FrameQueue.Count > _frameQueueCount)
                    {
                        token.WaitHandle.WaitOne(100);
                        continue;
                    }
                    if (token.IsCancellationRequested) break;

                    if (!_packetQueue.TryTake(out var ptr, 1000, token))
                        continue;

                    var pkt = (AVPacket*)ptr;
                    if (pkt == null) continue;

                    if (_codecs.TryGetValue(pkt->stream_index, out var codec) && codec.CodecInited)
                    {
                        var frames = codec.GetFramesFromPacket(pkt);
                        if (frames != null)
                            foreach (var frame in frames)
                                if (!FrameQueue.TryAdd(frame, 50, token))
                                {
                                    var temp = frame.frame;
                                    ffmpeg.av_frame_free(&temp);
                                }
                    }
                    ffmpeg.av_packet_free(&pkt);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                OnError?.Invoke(this, $"Decoder DecodeLoop crash: {ex.Message}");
            }
        }
        public void StopDecoding()
        {
            OnInfo?.Invoke(this, $"Decoder: Stop Decoding.");
            _ctsdecoder?.Cancel();
            if (_decodeTask != null)
                try { _decodeTask?.Wait(); } catch { }
            while (_packetQueue.TryTake(out var ptr))
            {
                AVPacket* pkt = (AVPacket*)ptr;
                ffmpeg.av_packet_free(&pkt);
            }
            lock (_lock)
            {
                _decodeTask = null;
            }
            _ctsdecoder?.Dispose();
            lock (_lock)
            {
                _ctsdecoder = null;
            }
        }
        void ClearCodecs()
        {
            foreach (var c in _codecs.Values)
                c.Dispose();
            _codecs.Clear();
        }


        public void Dispose()
        {
            StopDecoding();
            ClearCodecs();
        }
    }
}
