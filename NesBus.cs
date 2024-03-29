﻿/*
	olc::NES 
	"Thanks Dad for believing computers were gonna be a big deal..." - javidx9

	License (OLC-3)
	~~~~~~~~~~~~~~~

	Copyright 2018-2019 OneLoneCoder.com

	Redistribution and use in source and binary forms, with or without
	modification, are permitted provided that the following conditions
	are met:

	1. Redistributions or derivations of source code must retain the above
	copyright notice, this list of conditions and the following disclaimer.

	2. Redistributions or derivative works in binary form must reproduce
	the above copyright notice. This list of conditions and the following
	disclaimer must be reproduced in the documentation and/or other
	materials provided with the distribution.

	3. Neither the name of the copyright holder nor the names of its
	contributors may be used to endorse or promote products derived
	from this software without specific prior written permission.

	THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
	"AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
	LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
	A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
	HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
	SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
	LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
	DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
	THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
	(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
	OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.


	Relevant Video: https://youtu.be/xdzOvpYPmGE

	Links
	~~~~~
	YouTube:	https://www.youtube.com/javidx9
				https://www.youtube.com/javidx9extra
	Discord:	https://discord.gg/WhwHUMV
	Twitter:	https://www.twitter.com/javidx9
	Twitch:		https://www.twitch.tv/javidx9
	GitHub:		https://www.github.com/onelonecoder
	Patreon:	https://www.patreon.com/javidx9
	Homepage:	https://www.onelonecoder.com

	Author
	~~~~~~
	David Barr, aka javidx9, ©OneLoneCoder 2019

    Adapted to C# by davignola :  https://github.com/davignola
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NESharp.Components;
using NESharp.Components.Interfaces;

namespace NESharp
{
    public sealed class NesBus : IIODevice, IResetableDevice
    {
        private int SystemClockCounter { get; set; } = 0;
        // A simple form of Direct Memory Access is used to swiftly
        // transfer data from CPU bus memory into the OAM memory. It would
        // take too long to sensibly do this manually using a CPU loop, so
        // the program prepares a page of memory with the sprite info required
        // for the next frame and initiates a DMA transfer. This suspends the
        // CPU momentarily while the PPU gets sent data at PPU clock speeds.
        // Note here, that dma_page and dma_addr form a 16-bit address in 
        // the CPU bus address space
        private byte dma_page = 0x00;
        private byte dma_addr = 0x00;
        private byte dma_data = 0x00;

        private double dAudioTime = 0.0;
        private double dAudioGlobalTime = 0.0;
        private double dAudioTimePerNESClock = 0.0;
        private double dAudioTimePerSystemSample = 0.0f;

        // DMA transfers need to be timed accurately. In principle it takes
        // 512 cycles to read and write the 256 bytes of the OAM memory, a
        // read followed by a write. However, the CPU needs to be on an "even"
        // clock cycle, so a dummy cycle of idleness may be required
        private bool dma_dummy = true;

        // Finally a flag to indicate that a DMA transfer is happening
        private bool dma_transfer = false;

        // The 6502 derived processor
        public Cpu6502 Cpu6502 { get; private set; }

        // The 2C02 Picture Processing Unit
        public Ppu2C02 Ppu2C02 { get; private set; }

        // The "2A03" Audio Processing Unit
        public Apu2A03 Apu2A03 { get; private set; }

        public List<byte> Ram { get; private set; }
        public Cartridge Cartridge { get; private set; }

        // Synchronisation with system Audio
        public double dAudioSample = 0.0;

        // Controllers
        public byte[] Controller { get; set; }
        public byte[] ControllerState { get; set; }

        public List<IResetableDevice> Devices { get; private set; }

        public NesBus()
        {
            Devices = new List<IResetableDevice>();

            // Connect devices
            Cpu6502 = new Cpu6502();
            Cpu6502.ConnectBus(this);
            Devices.Add(Cpu6502);

            Ppu2C02 = new Ppu2C02();
            Ppu2C02.ConnectBus(this);
            Devices.Add(Ppu2C02);

            Apu2A03 = new Apu2A03();
            Apu2A03.ConnectBus(this);
            Devices.Add(Apu2A03);

            // init 2k RAM
            Ram = Enumerable.Repeat((byte)0, 2 * 1024).ToList();

            // Controlers
            Controller = new byte[] { 0x00, 0x00 };
            ControllerState = new byte[] { 0x00, 0x00 };
        }

        public void InsertCartridge(Cartridge cartridge)
        {
            Cartridge = cartridge;
            Ppu2C02.ConnectCartridge(cartridge);
            Devices.Add(Cartridge);
        }

        public void Reset(bool hardReset = false)
        {
            if (hardReset)
            {
                Ram = Enumerable.Repeat((byte)0, 2 * 1024).ToList();
            }

            Devices.ForEach(fe => fe.Reset(hardReset));

            SystemClockCounter = 0;
            dma_page = 0x00;
            dma_addr = 0x00;
            dma_data = 0x00;
            dma_dummy = true;
            dma_transfer = false;
        }

        public bool Clock()
        {
            // Clocking. The heart and soul of an emulator. The running
            // frequency is controlled by whatever calls this function.
            // So here we "divide" the clock as necessary and call
            // the peripheral devices clock() function at the correct
            // times.

            // The fastest clock frequency the digital system cares
            // about is equivalent to the PPU clock. So the PPU is clocked
            // each time this function is called...
            Ppu2C02.Clock();

            // ...also clock the APU
            Apu2A03.Clock();

            // The CPU runs 3 times slower than the PPU so we only call its
            // clock() function every 3 times this function is called. We
            // have a global counter to keep track of this.
            if (SystemClockCounter % 3 == 0)
            {
                // Is the system performing a DMA transfer form CPU memory to 
                // OAM memory on PPU?...
                if (dma_transfer)
                {
                    // ...Yes! We need to wait until the next even CPU clock cycle
                    // before it starts...
                    if (dma_dummy)
                    {
                        // ...So hang around in here each clock until 1 or 2 cycles
                        // have elapsed...
                        if (SystemClockCounter % 2 == 1)
                        {
                            // ...and finally allow DMA to start
                            dma_dummy = false;
                        }
                    }
                    else
                    {
                        // DMA can take place!
                        if (SystemClockCounter % 2 == 0)
                        {
                            // On even clock cycles, read from CPU bus
                            dma_data = CpuRead((ushort)(dma_page << 8 | dma_addr), false);
                        }
                        else
                        {
                            // On odd clock cycles, write to PPU OAM
                            Ppu2C02.OAM[dma_addr] = dma_data;
                            // Increment the lo byte of the address
                            dma_addr++;
                            // If this wraps around, we know that 256
                            // bytes have been written, so end the DMA
                            // transfer, and proceed as normal
                            if (dma_addr == 0x00)
                            {
                                dma_transfer = false;
                                dma_dummy = true;
                            }
                        }
                    }
                }
                else
                {
                    // No DMA happening, the CPU is in control of its
                    // own destiny. Go forth my friend and calculate
                    // awesomeness for many generations to come...
                    Cpu6502.Clock();
                }
            }

            // Synchronising with Audio
            bool bAudioSampleReady = false;
            dAudioTime += dAudioTimePerNESClock;
            if (dAudioTime >= dAudioTimePerSystemSample)
            {
                dAudioTime -= dAudioTimePerSystemSample;
                dAudioSample = Apu2A03.GetOutputSample();
                bAudioSampleReady = true;
            }


            // The PPU is capable of emitting an interrupt to indicate the
            // vertical blanking period has been entered. If it has, we need
            // to send that irq to the CPU.
            if (Ppu2C02.Nmi)
            {
                Ppu2C02.Nmi = false;
                Cpu6502.NMI();
            }

            // Check if cartridge is requesting IRQ
            if (Cartridge.GetMapper().IrqState)
            {
                Cartridge.GetMapper().IrqClear();
                Cpu6502.IRQ();
            }

            SystemClockCounter++;

            return bAudioSampleReady;
        }

        public void SetSampleFrequency(uint sample_rate)
        {
            dAudioTimePerSystemSample = 1.0 / (double)sample_rate;
            dAudioTimePerNESClock = 1.0 / 5369318.0; // PPU Clock Frequency
        }

        public void CpuWrite(ushort address, byte data)
        {
            // The cartridge "sees all" and has the facility to veto
            // the propagation of the bus transaction if it requires.
            // This allows the cartridge to map any address to some
            // other data, including the facility to divert transactions
            // with other physical devices. The NES does not do this
            // but I figured it might be quite a flexible way of adding
            // "custom" hardware to the NES in the future!
            if (Cartridge.CpuWrite(address, data)) { return; }

            if (address >= 0x0000 && address <= 0x1FFF)
            {
                // System RAM Address Range. The range covers 8KB, though
                // there is only 2KB available. That 2KB is "mirrored"
                // through this address range. Using bitwise AND to mask
                // the bottom 11 bits is the same as addr % 2048.
                Ram[address & 0x07FF] = data;
            }
            else if (address >= 0x2000 && address <= 0x3FFF)
            {
                // PPU Address range. The PPU only has 8 primary registers
                // and these are repeated throughout this range. We can
                // use bitwise AND operation to mask the bottom 3 bits, 
                // which is the equivalent of addr % 8.
                Ppu2C02.CpuWrite((ushort)(address & 0x0007), data);
            }
            else if ((address >= 0x4000 && address <= 0x4013) || address == 0x4015 || address == 0x4017) //  NES APU
            {
                Apu2A03.CpuWrite(address, data);
            }
            else if (address == 0x4014)
            {
                // A write to this address initiates a DMA transfer
                dma_page = data;
                dma_addr = 0x00;
                dma_transfer = true;
            }
            else if (address >= 0x4016 && address <= 0x4017)
            {
                // "Lock In" controller state at this time
                ControllerState[address & 0x0001] = Controller[address & 0x0001];
            }
        }

        public byte CpuRead(ushort address, bool asReadOnly)
        {
            byte data = 0x00;

            // Cartridge Address Range
            if (Cartridge.CpuRead(address, ref data)) { return data; }


            if (address >= 0x0000 && address <= 0x1FFF)
            {
                // System RAM Address Range, mirrored every 2048
                data = Ram[address & 0x07FF];
            }
            else if (address >= 0x2000 && address <= 0x3FFF)
            {
                // PPU Address range, mirrored every 8
                data = Ppu2C02.CpuRead((ushort)(address & 0x0007), asReadOnly);
            }
            else if (address == 0x4015)
            {
                // APU Read Status
                data = Apu2A03.CpuRead(address, asReadOnly);
            }
            else if (address >= 0x4016 && address <= 0x4017)
            {
                // Read out the MSB of the controller status word
                data = (ControllerState[address & 0x0001] & 0x80) > 0 ? (byte)1 : (byte)0;
                ControllerState[address & 0x0001] <<= 1;
            }

            return data;
        }
    }
}
