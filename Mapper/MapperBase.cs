using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NESharp.Mapper
{
    public abstract class MapperBase : IMapper
    {
        protected byte prgBanks { get; set; } = 0;
        protected byte chrBanks { get; set; } = 0;

        public MapperBase(byte prgBanks, byte chrBanks)
        {
            this.prgBanks = prgBanks;
            this.chrBanks = chrBanks;
        }

        public abstract bool CpuMapRead(ushort address, ref uint mapped_address);

        public abstract bool CpuMapWrite(ushort address, ref uint mapped_address);

        public abstract bool PpuMapRead(ushort address, ref uint mapped_address);

        public abstract bool PpuMapWrite(ushort address, ref uint mapped_address);
    }
}
