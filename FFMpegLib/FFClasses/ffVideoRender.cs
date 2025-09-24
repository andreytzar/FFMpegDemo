using FFmpeg.AutoGen;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FFMpegLib.FFClasses
{
    public unsafe class ffVideoRender : IDisposable
    {
        public event EventHandler<WriteableBitmap>? OnVideoChange;
        WriteableBitmap? _bitmap;
        SwsContext* _swsctx = null;
        AVFrame* _rgbframe = null;

        int _scale = ffmpeg.SWS_BILINEAR;

        int _swidth = 0;
        int _sheight = 0;
        int _sPixelFormat = -1;

        int _dwidth = 0;
        int _dheight = 0;
        int _dPixelFormat = (int)AVPixelFormat.AV_PIX_FMT_BGR0;

        readonly object _lock = new object();
        volatile bool _disposed = false;

        public void ProcessFrame(ffFrame fframe, CancellationToken token)
        {
            if (_disposed || token.IsCancellationRequested || fframe.MediaType != AVMediaType.AVMEDIA_TYPE_VIDEO) return;
            if (CheckSWS(fframe.frame))
            {
                var _frame = fframe.frame;

                if (ffmpeg.sws_scale(_swsctx, _frame->data, _frame->linesize, 0, _frame->height,
                    _rgbframe->data, _rgbframe->linesize) > 0)
                {
                    int stride = _rgbframe->linesize[0];
                    int bufSize = ffmpeg.av_image_get_buffer_size(
                        (AVPixelFormat)_rgbframe->format,
                        _rgbframe->width,
                        _rgbframe->height, 32);

                    if (bufSize > 0)
                        Application.Current.Dispatcher.BeginInvoke(() =>
                        {
                            try
                            {
                                if (_bitmap != null && !token.IsCancellationRequested &&! _disposed)
                                    _bitmap.WritePixels(new Int32Rect(0, 0, _dwidth, _dheight),
                                 (IntPtr)_rgbframe->data[0], bufSize, stride);
                            }
                            catch { }
                        });
                }
            }
        }

        bool CheckSWS(AVFrame* frame)
        {
            if (frame == null || frame->format < 0 || frame->width <= 0 || frame->height <= 0 || _disposed) return false;
            if (_swsctx == null || _bitmap == null || _rgbframe == null || _swidth != frame->width || _sheight != frame->height
                || _sPixelFormat != frame->format)
            {
                Free();
                if (_disposed) return false;
                lock (_lock)
                {
                    _swidth = frame->width;
                    _sheight = frame->height;
                    _dwidth = frame->width;
                    _dheight = frame->height;
                    _sPixelFormat = frame->format;
                }

                var sws = ffmpeg.sws_getContext(_swidth, _sheight, (AVPixelFormat)_sPixelFormat, _dwidth, _dheight,
                    (AVPixelFormat)_dPixelFormat, _scale, null, null, null);
                if (sws != null)
                {
                    var rgbFrame = ffmpeg.av_frame_alloc();
                    if (rgbFrame != null)
                    {
                        rgbFrame->format = _dPixelFormat;
                        rgbFrame->width = _dwidth;
                        rgbFrame->height = _dheight;

                        if (ffmpeg.av_frame_get_buffer(rgbFrame, 0) >= 0)
                        {
                            lock (_lock)
                            {
                                _swsctx = sws;
                                _rgbframe = rgbFrame;
                            }
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                if (_disposed) return;
                                _bitmap = new WriteableBitmap(_dwidth, _dheight, 96, 96, PixelFormats.Bgr32, null);
                                OnVideoChange?.Invoke(this, _bitmap);
                            });
                        }
                        else ffmpeg.av_frame_free(&rgbFrame);
                    }
                }
            }
            return _swsctx != null && _rgbframe != null && _bitmap != null && !_disposed;
        }

        void Free()
        {

            if (_rgbframe != null)
            {
                var temp = _rgbframe;
                ffmpeg.av_frame_free(&temp);
                _rgbframe = null;
            }
            if (_swsctx != null)
            {
                var temp = _swsctx;
                ffmpeg.sws_freeContext(temp);
                _swsctx = null;
            }
        }

        public void Dispose()
        {
            _disposed=true;
            Free();
        }
    }
}
