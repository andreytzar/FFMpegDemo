using FFmpeg.AutoGen;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace FFMpegLib.FFClasses
{
    public unsafe class ffRender : IDisposable
    {

        public event EventHandler<WriteableBitmap>? OnVideoChange
        {
            add { _videoRender.OnVideoChange += value; }
            remove { _videoRender.OnVideoChange -= value; }
        }

        public event EventHandler<string>? OnError;
        public event EventHandler<string>? OnInfo;


        ffVideoRender _videoRender = new();

        readonly BlockingCollection<ffFrame> _frameQueue;

        Task? _renderTask;
        CancellationTokenSource? _ctsrender;
        readonly object _lock = new();
        Stopwatch _clock = Stopwatch.StartNew();

        public ffRender(BlockingCollection<ffFrame> frameQueue)
        {
            _frameQueue = frameQueue;
        }

        public void StartRender()
        {
            StopRender();
            _ctsrender = new CancellationTokenSource();
            _renderTask = Task.Run(() => RenderLoop(_ctsrender.Token), _ctsrender.Token);
        }

        void RenderLoop(CancellationToken token)
        {

            _clock.Reset();
            try
            {
                while (!token.IsCancellationRequested)
                {
                    if (_frameQueue == null) break;

                    if (token.IsCancellationRequested) break;

                    if (!_frameQueue.TryTake(out var frame, 300, token))
                        continue;

                    if (frame != null && frame.frame != null)
                        ProcessFrame(frame, token);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                OnError?.Invoke(this, $"Render RenderLoop crash: {ex.Message}");
            }
        }

        void ProcessFrame(ffFrame frame, CancellationToken token)
        {
            if (frame.MediaType == AVMediaType.AVMEDIA_TYPE_VIDEO)
            {
                double ptsSec = 0;
                if (frame.frame->pts != ffmpeg.AV_NOPTS_VALUE)
                    ptsSec = (frame.frame->pts - frame.StartYime != ffmpeg.AV_NOPTS_VALUE ? frame.StartYime : 0) * ffmpeg.av_q2d(frame.TimeBase);

                double elapsed = _clock.Elapsed.TotalSeconds;
                double delay = ptsSec - elapsed;
                if (!token.IsCancellationRequested)
                {
                    if (delay > 0)
                        token.WaitHandle.WaitOne((int)(delay * 1000));
                    _videoRender.ProcessFrame(frame, token);
                }
            }
            frame.Dispose();
        }

        public void StopRender()
        {
            _ctsrender?.Cancel();
            if (_ctsrender != null)
                try
                {
                    var completed = Task.WhenAny(_renderTask!, Task.Delay(5000)).Result;
                    if (completed != _renderTask)
                        OnError?.Invoke(this, "StopRender timeout (render thread still running)");
                }
                catch { }
            while (_frameQueue.TryTake(out var frame))
            {
                frame.Dispose();
            }
            lock (_lock)
            {
                _renderTask = null;
            }
            _ctsrender?.Dispose();
            lock (_lock)
            {
                _ctsrender = null;
            }
        }


        public void Dispose()
        {
            StopRender();
            _videoRender.Dispose();
        }
    }
}
