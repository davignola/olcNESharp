using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NESharp.Components;

namespace NESharp.Mapper
{
    public abstract class MapperBase : IMapper
    {
        protected byte prgBanks { get; set; } = 0;
        protected byte chrBanks { get; set; } = 0;

        public virtual Cartridge.MIRROR Mirror { get; protected set; } = Cartridge.MIRROR.HARDWARE;
        public virtual bool IrqState { get; protected set; } = false;

        public MapperBase(byte prgBanks, byte chrBanks)
        {
            this.prgBanks = prgBanks;
            this.chrBanks = chrBanks;
        }

        public abstract bool CpuMapRead(ushort address, ref uint mapped_address, ref byte data);

        public abstract bool CpuMapWrite(ushort address, ref uint mapped_address, byte data);

        public abstract bool PpuMapRead(ushort address, ref uint mapped_address);

        public abstract bool PpuMapWrite(ushort address, ref uint mapped_address);
        public abstract void Reset();


        public virtual void IrqClear()
        {
            
        }

        public virtual void Scanline()
        {
            
        }
    }
}
