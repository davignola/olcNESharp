/*
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
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using olc.wrapper;

namespace NESharp.Components
{
    public sealed class Ppu2C02 : IConnectableDevice
    {

        private byte[][] tblName = new byte[2][];
        private byte[][] tblPattern = new byte[2][];
        private byte[] tblPalette = new byte[32];

        private PixelManaged[] palScreen = new PixelManaged[0x40];
        private SpriteManaged sprScreen = new SpriteManaged(256, 240);
        private SpriteManaged[] sprNameTable = { new SpriteManaged(256, 240), new SpriteManaged(256, 240) };
        private SpriteManaged[] sprPatternTable = { new SpriteManaged(128, 128), new SpriteManaged(128, 128) };

        private short scanline = 0;
        private short cycle = 0;

        private Cartridge cartridge;

        Random rand = new Random();

        public IIODevice Bus { get; private set; }
        public bool HasCompletedFrame { get; set; } = false;

        public Ppu2C02()
        {
            // Init screen 
            palScreen[0x00] = new PixelManaged(84, 84, 84);
            palScreen[0x01] = new PixelManaged(0, 30, 116);
            palScreen[0x02] = new PixelManaged(8, 16, 144);
            palScreen[0x03] = new PixelManaged(48, 0, 136);
            palScreen[0x04] = new PixelManaged(68, 0, 100);
            palScreen[0x05] = new PixelManaged(92, 0, 48);
            palScreen[0x06] = new PixelManaged(84, 4, 0);
            palScreen[0x07] = new PixelManaged(60, 24, 0);
            palScreen[0x08] = new PixelManaged(32, 42, 0);
            palScreen[0x09] = new PixelManaged(8, 58, 0);
            palScreen[0x0A] = new PixelManaged(0, 64, 0);
            palScreen[0x0B] = new PixelManaged(0, 60, 0);
            palScreen[0x0C] = new PixelManaged(0, 50, 60);
            palScreen[0x0D] = new PixelManaged(0, 0, 0);
            palScreen[0x0E] = new PixelManaged(0, 0, 0);
            palScreen[0x0F] = new PixelManaged(0, 0, 0);

            palScreen[0x10] = new PixelManaged(152, 150, 152);
            palScreen[0x11] = new PixelManaged(8, 76, 196);
            palScreen[0x12] = new PixelManaged(48, 50, 236);
            palScreen[0x13] = new PixelManaged(92, 30, 228);
            palScreen[0x14] = new PixelManaged(136, 20, 176);
            palScreen[0x15] = new PixelManaged(160, 20, 100);
            palScreen[0x16] = new PixelManaged(152, 34, 32);
            palScreen[0x17] = new PixelManaged(120, 60, 0);
            palScreen[0x18] = new PixelManaged(84, 90, 0);
            palScreen[0x19] = new PixelManaged(40, 114, 0);
            palScreen[0x1A] = new PixelManaged(8, 124, 0);
            palScreen[0x1B] = new PixelManaged(0, 118, 40);
            palScreen[0x1C] = new PixelManaged(0, 102, 120);
            palScreen[0x1D] = new PixelManaged(0, 0, 0);
            palScreen[0x1E] = new PixelManaged(0, 0, 0);
            palScreen[0x1F] = new PixelManaged(0, 0, 0);

            palScreen[0x20] = new PixelManaged(236, 238, 236);
            palScreen[0x21] = new PixelManaged(76, 154, 236);
            palScreen[0x22] = new PixelManaged(120, 124, 236);
            palScreen[0x23] = new PixelManaged(176, 98, 236);
            palScreen[0x24] = new PixelManaged(228, 84, 236);
            palScreen[0x25] = new PixelManaged(236, 88, 180);
            palScreen[0x26] = new PixelManaged(236, 106, 100);
            palScreen[0x27] = new PixelManaged(212, 136, 32);
            palScreen[0x28] = new PixelManaged(160, 170, 0);
            palScreen[0x29] = new PixelManaged(116, 196, 0);
            palScreen[0x2A] = new PixelManaged(76, 208, 32);
            palScreen[0x2B] = new PixelManaged(56, 204, 108);
            palScreen[0x2C] = new PixelManaged(56, 180, 204);
            palScreen[0x2D] = new PixelManaged(60, 60, 60);
            palScreen[0x2E] = new PixelManaged(0, 0, 0);
            palScreen[0x2F] = new PixelManaged(0, 0, 0);

            palScreen[0x30] = new PixelManaged(236, 238, 236);
            palScreen[0x31] = new PixelManaged(168, 204, 236);
            palScreen[0x32] = new PixelManaged(188, 188, 236);
            palScreen[0x33] = new PixelManaged(212, 178, 236);
            palScreen[0x34] = new PixelManaged(236, 174, 236);
            palScreen[0x35] = new PixelManaged(236, 174, 212);
            palScreen[0x36] = new PixelManaged(236, 180, 176);
            palScreen[0x37] = new PixelManaged(228, 196, 144);
            palScreen[0x38] = new PixelManaged(204, 210, 120);
            palScreen[0x39] = new PixelManaged(180, 222, 120);
            palScreen[0x3A] = new PixelManaged(168, 226, 144);
            palScreen[0x3B] = new PixelManaged(152, 226, 180);
            palScreen[0x3C] = new PixelManaged(160, 214, 228);
            palScreen[0x3D] = new PixelManaged(160, 162, 160);
            palScreen[0x3E] = new PixelManaged(0, 0, 0);
            palScreen[0x3F] = new PixelManaged(0, 0, 0);
        }

        public void ConnectBus(IIODevice bus)
        {
            this.Bus = bus;
        }

        public SpriteManaged GetScreen()
        {
            return sprScreen;
        }

        public SpriteManaged GetNameTable(byte i)
        {
            return sprNameTable[i];
        }

        public SpriteManaged GetPatternTable(byte i)
        {
            return sprPatternTable[i];
        }

        public byte CpuRead(ushort address)
        {
            byte data = 0x00;

            switch (address)
            {
                case 0x0000: // Control
                    break;
                case 0x0001: // Mask
                    break;
                case 0x0002: // Status
                    break;
                case 0x0003: // OAM Address
                    break;
                case 0x0004: // OAM Data
                    break;
                case 0x0005: // Scroll
                    break;
                case 0x0006: // PPU Address
                    break;
                case 0x0007: // PPU Data
                    break;
            }

            return data;
        }

        public void CpuWrite(ushort address, byte data)
        {
            switch (address)
            {
                case 0x0000: // Control
                    break;
                case 0x0001: // Mask
                    break;
                case 0x0002: // Status
                    break;
                case 0x0003: // OAM Address
                    break;
                case 0x0004: // OAM Data
                    break;
                case 0x0005: // Scroll
                    break;
                case 0x0006: // PPU Address
                    break;
                case 0x0007: // PPU Data
                    break;
            }
        }

        public byte PpuRead(ushort address)
        {
            byte data = 0x00;

            address &= 0x3FFF;

            if (cartridge.PpuRead(address, ref data))
            {

            }

            return data;
        }

        public void PpuWrite(ushort address, byte data)
        {
            address &= 0x3FFF;

            if (cartridge.CpuWrite(address, data))
            {

            }
        }

        public void ConnectCartridge(Cartridge cartridge)
        {
            this.cartridge = cartridge;
        }

        public void Clock()
        {
            

            // Fake some noise for now
            sprScreen.SetPixel(cycle - 1, scanline, palScreen[(rand.Next() % 2 != 0) ? 0x3F : 0x30]);

            // Advance renderer - it never stops, it's relentless
            cycle++;
            if (cycle >= 341)
            {
                cycle = 0;
                scanline++;
                if (scanline >= 261)
                {
                    scanline = -1;
                    HasCompletedFrame = true;
                }
            }
        }



    }
}
