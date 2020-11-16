using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NESharp.Chip;

namespace NESharp
{
    public sealed class NesBus : IIODevice
    {
        public Cpu6502 Cpu6502 { get; private set; }
        public List<byte> Ram { get; private set; }

        public NesBus()
        {
            // Connect devices
            Cpu6502 = new Cpu6502();
            Cpu6502.ConnectBus(this);

            // init RAM
            Ram = Enumerable.Repeat((byte)0, 64 * 1024).ToList();
        }

        public void Write(ushort address, byte data)
        {
            if (address >= 0x0000 && address <= 0xFFFF)
            {
                Ram[address] = data;
            }
        }

        public byte Read(ushort address)
        {
            if (address >= 0x0000 && address <= 0xFFFF)
            {
                return Ram[address];
            }

            return 0x00;
        }
    }
}
