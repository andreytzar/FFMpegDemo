//using FFMpegLib.FFClasses;
//using System;
//using System.IO;
//using System.Windows.Media.Imaging;


//namespace FFMpegLib
//{
//    public unsafe class FFPlayer:IDisposable
//    {
//        public event EventHandler<string>? OnError { add {_context.OnError+= value;}  remove { _context.OnError -= value; } }
//        public event EventHandler<WriteableBitmap>? OnVideoBitmapChange { add { _context.OnVideoBitmapChange += value; } remove { _context.OnVideoBitmapChange -= value; } }
//        FFContext _context = new();
//        object _lock = new();
//        public List<FFStream> Streams => _context.Streams.Values.ToList();
//        public FFPlayer()
//        {
//            _=Ffmpg.Instance;
//        }

//        public bool OpenFile(string file)
//        {
//            string _file=file.Trim();
//            if (!string.IsNullOrEmpty(_file) && File.Exists(_file))
//                return _context.Open(_file);
//            return false;
//        }

//        public void Play()=>_context.Play();
//        public void Stop() => _context.Close();
//        public void Dispose()
//        {
//            _context.Dispose();
//        }
//    }
//}
