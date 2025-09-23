using FFmpeg.AutoGen;
using FFMpegLib.Helpers;
using FFMpegLib.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Shapes;

namespace FFMpegLib.FFClasses
{
    public unsafe class ffDemuxer : IDisposable
    {
        static int _packetQueueCount = 50;
        public bool IsOpen { get => _IsOpen; }
        public string? CurrentFilePath { get; private set; } = string.Empty;
        public ffDemuxerInfo? DemuxerInfo { get; private set; }

        public event EventHandler<string>? OnError;
        public event EventHandler<string>? OnInfo;

        internal BlockingCollection<IntPtr> PacketQueue { get; private set; } = new BlockingCollection<IntPtr>();


        Task? _readerTask;
        AVFormatContext* _fmtCtx = null;

        volatile bool _IsOpen = false;
        readonly object _lock = new();
        CancellationTokenSource? _ctsreader;

        public bool OpenFile(string file)
        {
            Info($"Openning {file}");
            file = file.Trim();
            if (!string.IsNullOrEmpty(file) && File.Exists(file) && Open(file))
            {
                Info($"File {file} openned");
                return true;
            }
            else Error($"Could not open {file}");
            return false;
        }

        bool Open(string file)
        {
            Close();
            AVFormatContext* fmt = null;

            int ret = ffmpeg.avformat_open_input(&fmt, file, null, null);
            if (ret >= 0 && fmt != null)
            {
                ret = ffmpeg.avformat_find_stream_info(fmt, null);
                if (ret >= 0)
                {
                    lock (_lock)
                    {
                        _fmtCtx = fmt;
                        CurrentFilePath = file;
                        DemuxerInfo = ffDemuxerInfoExtractor.FromFormatContext(fmt, CurrentFilePath);
                        _IsOpen = true;
                    }
                    return _IsOpen;
                }
                else
                {
                    ffmpeg.avformat_close_input(&fmt);
                    Error($"Cannot find stream info: {ret.av_errorToString()}");
                }
            }
            else Error($"Cannot open input: {ret.av_errorToString()}");

            return false;
        }
        public void StartReading()
        {
            StopReading();
            if (!_IsOpen || _fmtCtx == null) return;
            _ctsreader = new CancellationTokenSource();
            Info("Sart Reading");
            _readerTask = Task.Run(() => ReadLoop(_ctsreader.Token), _ctsreader.Token);

        }

        void ReadLoop(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    if (PacketQueue.Count > _packetQueueCount)
                    {
                        token.WaitHandle.WaitOne(100);
                        continue;
                    }
                    AVPacket* pkt = ffmpeg.av_packet_alloc();
                    if (pkt == null) continue;

                    int ret = ffmpeg.av_read_frame(_fmtCtx, pkt);
                    if (ret < 0)
                    {
                        // кінець файлу або помилка
                        ffmpeg.av_packet_free(&pkt);
                        break;
                    }
                    PacketQueue.Add((IntPtr)pkt, token);
                }
            }
            catch (OperationCanceledException) { }
        }

        public void StopReading()
        {
            _ctsreader?.Cancel();
            if (_readerTask != null) Info("Stop Reading");
            try { _readerTask?.Wait(); } catch { }

            while (PacketQueue.TryTake(out var ptr))
            {
                AVPacket* pkt = (AVPacket*)ptr;
                ffmpeg.av_packet_free(&pkt);
            }
            _ctsreader?.Dispose();

            lock (_lock)
            {
                _readerTask = null;
                _ctsreader = null;
            }
        }

        public void Close()
        {
            var open = _IsOpen;
            _IsOpen = false;
            CurrentFilePath = string.Empty;
            StopReading();

            lock (_lock)
            {
                if (_fmtCtx != null)
                {
                    var ctx = _fmtCtx;
                    ffmpeg.avformat_close_input(&ctx);
                    _fmtCtx = null;
                }
                DemuxerInfo = null;
            }
            if (open) Info("File closed");
        }

        void Error(string text) => OnError?.Invoke(this, text);
        void Info(string text) => OnInfo?.Invoke(this, text);

        public void Dispose()
        {
            Close();
            PacketQueue.Dispose();
        }
    }
}
