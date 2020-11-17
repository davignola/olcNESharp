﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NESharp.Components
{
    public interface IIODevice
    {
        void CpuWrite(ushort address, byte data);
        byte CpuRead(ushort address);
    }
}
