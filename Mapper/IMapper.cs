using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NESharp.Mapper
{
    interface IMapper
    {
        bool CpuMapRead(ushort address, ref uint mapped_address);
        bool CpuMapWrite(ushort address, ref uint mapped_address);
        bool PpuMapRead(ushort address, ref uint mapped_address);
        bool PpuMapWrite(ushort address, ref uint mapped_address);
        void Reset();
    }
}
