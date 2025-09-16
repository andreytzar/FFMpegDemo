using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FFMpegLib
{
    public unsafe class FFPlayer:IDisposable
    {
        object _lock = new();

        public FFPlayer()
        {
            _=Ffmpg.Instance;
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
