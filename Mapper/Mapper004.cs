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
using System.Text;
using System.Threading.Tasks;
using NESharp.Components;

namespace NESharp.Mapper
{
    public class Mapper004 : MapperBase
    {

        byte nTargetRegister = 0x00;
        bool bPRGBankMode = false;
        bool bCHRInversion = false;

        private uint[] pRegister;
        private uint[] pCHRBank;
        private uint[] pPRGBank;

        bool bIRQActive = false;
        bool bIRQEnable = false;
        bool bIRQUpdate = false;
        ushort nIRQCounter = 0x0000;
        ushort nIRQReload = 0x0000;

        byte[] vRAMStatic;



        public Mapper004(byte prgBanks, byte chrBanks) : base(prgBanks, chrBanks)
        {
            vRAMStatic = new byte[32 * 1024];
            pRegister = new uint[8];
            pCHRBank = new uint[8];
            pPRGBank = new uint[4];
        }


        public override bool CpuMapRead(ushort address, ref uint mapped_address, ref byte data)
        {
            if (address >= 0x6000 && address <= 0x7FFF)
            {
                // Write is to static ram on cartridge
                mapped_address = 0xFFFFFFFF;

                // Write data to RAM
                data = vRAMStatic[address & 0x1FFF];

                // Signal mapper has handled request
                return true;
            }


            if (address >= 0x8000 && address <= 0x9FFF)
            {
                mapped_address = (uint)(pPRGBank[0] + (address & 0x1FFF));
                return true;
            }

            if (address >= 0xA000 && address <= 0xBFFF)
            {
                mapped_address = (uint)(pPRGBank[1] + (address & 0x1FFF));
                return true;
            }

            if (address >= 0xC000 && address <= 0xDFFF)
            {
                mapped_address = (uint)(pPRGBank[2] + (address & 0x1FFF));
                return true;
            }

            if (address >= 0xE000 && address <= 0xFFFF)
            {
                mapped_address = (uint)(pPRGBank[3] + (address & 0x1FFF));
                return true;
            }

            return false;
        }

        public override bool CpuMapWrite(ushort address, ref uint mapped_address, byte data)
        {
            if (address >= 0x6000 && address <= 0x7FFF)
            {
                // Write is to static ram on cartridge
                mapped_address = 0xFFFFFFFF;

                // Write data to RAM
                vRAMStatic[address & 0x1FFF] = data;

                // Signal mapper has handled request
                return true;
            }

            if (address >= 0x8000 && address <= 0x9FFF)
            {
                // Bank Select
                if ((address & 0x0001) == 0)
                {
                    nTargetRegister = (byte)(data & 0x07);
                    bPRGBankMode = (data & 0x40) != 0;
                    bCHRInversion = (data & 0x80) != 0;
                }
                else
                {
                    // Update target register
                    pRegister[nTargetRegister] = data;

                    // Update Pointer Table
                    if (bCHRInversion)
                    {
                        pCHRBank[0] = pRegister[2] * 0x0400;
                        pCHRBank[1] = pRegister[3] * 0x0400;
                        pCHRBank[2] = pRegister[4] * 0x0400;
                        pCHRBank[3] = pRegister[5] * 0x0400;
                        pCHRBank[4] = (pRegister[0] & 0xFE) * 0x0400;
                        pCHRBank[5] = pRegister[0] * 0x0400 + 0x0400;
                        pCHRBank[6] = (pRegister[1] & 0xFE) * 0x0400;
                        pCHRBank[7] = pRegister[1] * 0x0400 + 0x0400;
                    }
                    else
                    {
                        pCHRBank[0] = (pRegister[0] & 0xFE) * 0x0400;
                        pCHRBank[1] = pRegister[0] * 0x0400 + 0x0400;
                        pCHRBank[2] = (pRegister[1] & 0xFE) * 0x0400;
                        pCHRBank[3] = pRegister[1] * 0x0400 + 0x0400;
                        pCHRBank[4] = pRegister[2] * 0x0400;
                        pCHRBank[5] = pRegister[3] * 0x0400;
                        pCHRBank[6] = pRegister[4] * 0x0400;
                        pCHRBank[7] = pRegister[5] * 0x0400;
                    }

                    if (bPRGBankMode)
                    {
                        pPRGBank[2] = (pRegister[6] & 0x3F) * 0x2000;
                        pPRGBank[0] = (uint)((prgBanks * 2 - 2) * 0x2000);
                    }
                    else
                    {
                        pPRGBank[0] = (pRegister[6] & 0x3F) * 0x2000;
                        pPRGBank[2] = (uint)((prgBanks * 2 - 2) * 0x2000);
                    }

                    pPRGBank[1] = (pRegister[7] & 0x3F) * 0x2000;
                    pPRGBank[3] = (uint)((prgBanks * 2 - 1) * 0x2000);

                }

                return false;
            }

            if (address >= 0xA000 && address <= 0xBFFF)
            {
                if ((address & 0x0001) == 0)
                {
                    // Mirroring
                    if ((data & 0x01) != 0)
                        Mirror = Cartridge.MIRROR.HORIZONTAL;
                    else
                        Mirror = Cartridge.MIRROR.VERTICAL;
                }
                else
                {
                    // PRG Ram Protect
                    // TODO:
                }
                return false;
            }

            if (address >= 0xC000 && address <= 0xDFFF)
            {
                if ((address & 0x0001) == 0)
                {
                    nIRQReload = data;
                }
                else
                {
                    nIRQCounter = 0x0000;
                }
                return false;
            }

            if (address >= 0xE000 && address <= 0xFFFF)
            {
                if ((address & 0x0001) == 0)
                {
                    bIRQEnable = false;
                    bIRQActive = false;
                }
                else
                {
                    bIRQEnable = true;
                }
                return false;
            }



            return false;
        }

        public override bool PpuMapRead(ushort address, ref uint mapped_address)
        {
            if (address >= 0x0000 && address <= 0x03FF)
            {
                mapped_address = (uint)(pCHRBank[0] + (address & 0x03FF));
                return true;
            }

            if (address >= 0x0400 && address <= 0x07FF)
            {
                mapped_address = (uint)(pCHRBank[1] + (address & 0x03FF));
                return true;
            }

            if (address >= 0x0800 && address <= 0x0BFF)
            {
                mapped_address = (uint)(pCHRBank[2] + (address & 0x03FF));
                return true;
            }

            if (address >= 0x0C00 && address <= 0x0FFF)
            {
                mapped_address = (uint)(pCHRBank[3] + (address & 0x03FF));
                return true;
            }

            if (address >= 0x1000 && address <= 0x13FF)
            {
                mapped_address = (uint)(pCHRBank[4] + (address & 0x03FF));
                return true;
            }

            if (address >= 0x1400 && address <= 0x17FF)
            {
                mapped_address = (uint)(pCHRBank[5] + (address & 0x03FF));
                return true;
            }

            if (address >= 0x1800 && address <= 0x1BFF)
            {
                mapped_address = (uint)(pCHRBank[6] + (address & 0x03FF));
                return true;
            }

            if (address >= 0x1C00 && address <= 0x1FFF)
            {
                mapped_address = (uint)(pCHRBank[7] + (address & 0x03FF));
                return true;
            }

            return false;
        }

        public override bool PpuMapWrite(ushort address, ref uint mapped_address)
        {
            return false;
        }

        public override void Reset()
        {
            nTargetRegister = 0x00;
            bPRGBankMode = false;
            bCHRInversion = false;
            Mirror = Cartridge.MIRROR.HORIZONTAL;

            bIRQActive = false;
            bIRQEnable = false;
            bIRQUpdate = false;
            nIRQCounter = 0x0000;
            nIRQReload = 0x0000;

            for (int i = 0; i < 4; i++) pPRGBank[i] = 0;
            for (int i = 0; i < 8; i++) { pCHRBank[i] = 0; pRegister[i] = 0; }

            pPRGBank[0] = 0 * 0x2000;
            pPRGBank[1] = 1 * 0x2000;
            pPRGBank[2] = (uint)((prgBanks * 2 - 2) * 0x2000);
            pPRGBank[3] = (uint)((prgBanks * 2 - 1) * 0x2000);
        }

        public override bool IrqState
        {
            get { return bIRQActive; }
        }

        public override void IrqClear()
        {
            bIRQActive = false;
        }

        public override void Scanline()
        {
            if (nIRQCounter == 0)
            {
                nIRQCounter = nIRQReload;
            }
            else
                nIRQCounter--;

            if (nIRQCounter == 0 && bIRQEnable)
            {
                bIRQActive = true;
            }
        }
    }
}
