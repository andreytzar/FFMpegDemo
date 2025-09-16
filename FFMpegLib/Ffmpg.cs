using FFmpeg.AutoGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace FFMpegLib
{
    public unsafe class Ffmpg
    {
        static Lazy<Ffmpg> _instance= new Lazy<Ffmpg>(() => new Ffmpg());
        public static Ffmpg  Instance=>_instance.Value;
        int _LogLevel = ffmpeg.AV_LOG_ERROR;
        public int LogLevel { get => _LogLevel; set { if (_LogLevel != value) { _LogLevel = value; ffmpeg.av_log_set_level(value); } } }

        static string ffpath = Path.Combine(AppContext.BaseDirectory, "FFMPEG");
        static string logPath =Path.Combine(AppContext.BaseDirectory, "ffmpeg.log");
        static av_log_set_callback_callback? _logCallback;
        static object _lock= new object();

        Ffmpg()
        {
            ffmpeg.RootPath = ffpath;
            RegisterFFmpegLog();
            ffmpeg.avdevice_register_all();
            ffmpeg.avformat_network_init();
        }

        static void RegisterFFmpegLog()
        {
            if (File.Exists(logPath)) 
                File.Delete(logPath);
            
            _logCallback = new av_log_set_callback_callback(LogCallback);
            ffmpeg.av_log_set_level(ffmpeg.AV_LOG_ERROR);
            ffmpeg.av_log_set_callback(_logCallback);
        }

        static void LogCallback(void* ptr, int level, string format, byte* vl)
        {
            if (level > ffmpeg.av_log_get_level())
                return;
            try
            {
                var lineBuffer = stackalloc byte[1024];
                ffmpeg.av_log_format_line(ptr, level, format, vl, lineBuffer, 1024, null);
                string message = Marshal.PtrToStringAnsi((IntPtr)lineBuffer) ?? "";
                lock (_lock)
                {
                    File.AppendAllText(logPath, $"{DateTime.Now:yy-MM-dd HH:mm:ss} [{level}] {message}");
                }
            }
            catch { }
        }

    }
}
