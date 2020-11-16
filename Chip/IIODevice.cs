using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NESharp.Chip
{
    public interface IIODevice
    {
        void Write(ushort address, byte data);
        byte Read(ushort address);
    }
}
