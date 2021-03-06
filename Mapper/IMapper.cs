﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NESharp.Components;
using NESharp.Components.Interfaces;

namespace NESharp.Mapper
{
    public interface IMapper : IResetableDevice
    {
        bool CpuMapRead(ushort address, ref uint mapped_address, ref byte data);
        bool CpuMapWrite(ushort address, ref uint mapped_address, byte data);
        bool PpuMapRead(ushort address, ref uint mapped_address);
        bool PpuMapWrite(ushort address, ref uint mapped_address);

        Cartridge.MIRROR Mirror { get; }

        // IRQ Interface
        bool IrqState { get; }
        void IrqClear();

        // Scanline Counting
        void Scanline();
    }
}
