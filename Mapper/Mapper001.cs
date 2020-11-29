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
    public class Mapper001 : MapperBase
    {

        byte nCHRBankSelect4Lo = 0x00;
        byte nCHRBankSelect4Hi = 0x00;
        byte nCHRBankSelect8 = 0x00;

        byte nPRGBankSelect16Lo = 0x00;
        byte nPRGBankSelect16Hi = 0x00;
        byte nPRGBankSelect32 = 0x00;

        byte nLoadRegister = 0x00;
        byte nLoadRegisterCount = 0x00;
        byte nControlRegister = 0x00;

        byte[] vRAMStatic;

        public Mapper001(byte prgBanks, byte chrBanks) : base(prgBanks, chrBanks)
        {
            vRAMStatic = new byte[32 * 1024];
        }


        public override bool CpuMapRead(ushort address, ref uint mapped_address, ref byte data)
        {
            if (address >= 0x6000 && address <= 0x7FFF)
            {
                // Read is from static ram on cartridge
                mapped_address = 0xFFFFFFFF;

                // Read data from RAM
                data = vRAMStatic[address & 0x1FFF];

                // Signal mapper has handled request
                return true;
            }

            if (address >= 0x8000)
            {
                if ((nControlRegister & 0b01000) != 0)
                {
                    // 16K Mode
                    if (address >= 0x8000 && address <= 0xBFFF)
                    {
                        mapped_address = (uint)(nPRGBankSelect16Lo * 0x4000 + (address & 0x3FFF));
                        return true;
                    }

                    if (address >= 0xC000 && address <= 0xFFFF)
                    {
                        mapped_address = (uint)(nPRGBankSelect16Hi * 0x4000 + (address & 0x3FFF));
                        return true;
                    }
                }
                else
                {
                    // 32K Mode
                    mapped_address = (uint)(nPRGBankSelect32 * 0x8000 + (address & 0x7FFF));
                    return true;
                }
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

            if (address >= 0x8000)
            {
                if ((data & 0x80) != 0)
                {
                    // MSB is set, so reset serial loading
                    nLoadRegister = 0x00;
                    nLoadRegisterCount = 0;
                    nControlRegister = (byte)(nControlRegister | 0x0C);
                }
                else
                {
                    // Load data in serially into load register
                    // It arrives LSB first, so implant this at
                    // bit 5. After 5 writes, the register is ready
                    nLoadRegister >>= 1;
                    nLoadRegister |= (byte)((data & 0x01) << 4);
                    nLoadRegisterCount++;

                    if (nLoadRegisterCount == 5)
                    {
                        // Get Mapper Target Register, by examining
                        // bits 13 & 14 of the address
                        byte nTargetRegister = (byte)((address >> 13) & 0x03);

                        if (nTargetRegister == 0) // 0x8000 - 0x9FFF
                        {
                            // Set Control Register
                            nControlRegister = (byte)(nLoadRegister & 0x1F);

                            switch (nControlRegister & 0x03)
                            {
                                case 0: Mirror = Cartridge.MIRROR.ONESCREEN_LO; break;
                                case 1: Mirror = Cartridge.MIRROR.ONESCREEN_HI; break;
                                case 2: Mirror = Cartridge.MIRROR.VERTICAL; break;
                                case 3: Mirror = Cartridge.MIRROR.HORIZONTAL; break;
                            }
                        }
                        else if (nTargetRegister == 1) // 0xA000 - 0xBFFF
                        {
                            // Set CHR Bank Lo
                            if ((nControlRegister & 0b10000) != 0)
                            {
                                // 4K CHR Bank at PPU 0x0000
                                nCHRBankSelect4Lo = (byte)(nLoadRegister & 0x1F);
                            }
                            else
                            {
                                // 8K CHR Bank at PPU 0x0000
                                nCHRBankSelect8 = (byte)(nLoadRegister & 0x1E);
                            }
                        }
                        else if (nTargetRegister == 2) // 0xC000 - 0xDFFF
                        {
                            // Set CHR Bank Hi
                            if ((nControlRegister & 0b10000) != 0)
                            {
                                // 4K CHR Bank at PPU 0x1000
                                nCHRBankSelect4Hi = (byte)(nLoadRegister & 0x1F);
                            }
                        }
                        else if (nTargetRegister == 3) // 0xE000 - 0xFFFF
                        {
                            // Configure PRG Banks
                            byte nPRGMode = (byte)((nControlRegister >> 2) & 0x03);

                            if (nPRGMode == 0 || nPRGMode == 1)
                            {
                                // Set 32K PRG Bank at CPU 0x8000
                                nPRGBankSelect32 = (byte)((nLoadRegister & 0x0E) >> 1);
                            }
                            else if (nPRGMode == 2)
                            {
                                // Fix 16KB PRG Bank at CPU 0x8000 to First Bank
                                nPRGBankSelect16Lo = 0;
                                // Set 16KB PRG Bank at CPU 0xC000
                                nPRGBankSelect16Hi = (byte)(nLoadRegister & 0x0F);
                            }
                            else if (nPRGMode == 3)
                            {
                                // Set 16KB PRG Bank at CPU 0x8000
                                nPRGBankSelect16Lo = (byte)(nLoadRegister & 0x0F);
                                // Fix 16KB PRG Bank at CPU 0xC000 to Last Bank
                                nPRGBankSelect16Hi = (byte)(prgBanks - 1);
                            }
                        }

                        // 5 bits were written, and decoded, so
                        // reset load register
                        nLoadRegister = 0x00;
                        nLoadRegisterCount = 0;
                    }

                }

            }

            // Mapper has handled write, but do not update ROMs
            return false;
        }

        public override bool PpuMapRead(ushort address, ref uint mapped_address)
        {
            if (address < 0x2000)
            {
                if (chrBanks == 0)
                {
                    mapped_address = address;
                    return true;
                }
                else
                {
                    if ((nControlRegister & 0b10000) != 0)
                    {
                        // 4K CHR Bank Mode
                        if (address >= 0x0000 && address <= 0x0FFF)
                        {
                            mapped_address = (uint)(nCHRBankSelect4Lo * 0x1000 + (address & 0x0FFF));
                            return true;
                        }

                        if (address >= 0x1000 && address <= 0x1FFF)
                        {
                            mapped_address = (uint)(nCHRBankSelect4Hi * 0x1000 + (address & 0x0FFF));
                            return true;
                        }
                    }
                    else
                    {
                        // 8K CHR Bank Mode
                        mapped_address = (uint)(nCHRBankSelect8 * 0x2000 + (address & 0x1FFF));
                        return true;
                    }
                }
            }

            return false;
        }

        public override bool PpuMapWrite(ushort address, ref uint mapped_address)
        {
            if (address < 0x2000)
            {
                if (chrBanks == 0)
                {
                    mapped_address = address;
                    return true;
                }

                return true;
            }
            else
                return false;
        }

        public override void Reset(bool hardReset = false)
        {
            if (hardReset)
            {
                Array.Clear(vRAMStatic,0,vRAMStatic.Length);
            }

            nControlRegister = 0x1C;
            nLoadRegister = 0x00;
            nLoadRegisterCount = 0x00;

            nCHRBankSelect4Lo = 0;
            nCHRBankSelect4Hi = 0;
            nCHRBankSelect8 = 0;

            nPRGBankSelect32 = 0;
            nPRGBankSelect16Lo = 0;
            nPRGBankSelect16Hi = (byte)(prgBanks - 1);
        }
    }
}
