using FFmpeg.AutoGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace FFMpegLib.Helpers
{
    public unsafe static class ffUtils
    {

        public static string UIntToString(uint tag)
        {
            var bytes = BitConverter.GetBytes(tag);
            return Encoding.ASCII.GetString(bytes).TrimEnd('\0');
        }

        public static string PtrToStringUTF8(byte* ptr)
        {
            if (ptr == null) return string.Empty;
            return Encoding.UTF8.GetString(MemoryMarshal.AsBytes(new ReadOnlySpan<byte>(ptr, strlen(ptr))));
        }

        public static unsafe string av_errorToString(this int error)
        {
            var bufferSize = 1024;
            var buffer = stackalloc byte[bufferSize];
            if (ffmpeg.av_strerror(error, buffer, (ulong)bufferSize) == 0)
            {
                var message = Marshal.PtrToStringAnsi((IntPtr)buffer);
                return message ?? string.Empty;
            }
            return string.Empty;
        }

        private static int strlen(byte* ptr)
        {
            int length = 0;
            while (ptr[length] != 0) length++;
            return length;
        }
    }
}
