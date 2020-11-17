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

namespace NESharp.Mapper
{
    public class Mapper000 : MapperBase
    {
        public Mapper000(byte prgBanks, byte chrBanks) : base(prgBanks, chrBanks)
        {
        }

        /// <summary>
        /// if PRGROM is 16KB
        ///     CPU Address Bus          PRG ROM
        ///     0x8000 -> 0xBFFF: Map    0x0000 -> 0x3FFF
        ///     0xC000 -> 0xFFFF: Mirror 0x0000 -> 0x3FFF
        /// if PRGROM is 32KB
        ///     CPU Address Bus          PRG ROM
        ///     0x8000 -> 0xFFFF: Map    0x0000 -> 0x7FFF
        /// </summary>
        /// <param name="address"></param>
        /// <param name="mapped_address"></param>
        /// <returns></returns>
        private bool MapCpu(ushort address, ref uint mapped_address)
        {
            if (address >= 0x8000 && address <= 0xFFFF)
            {
                mapped_address = (uint)(address & (prgBanks > 1 ? 0x7FFF : 0x3FFF));
                return true;
            }

            return false;
        }

        public override bool CpuMapRead(ushort address, ref uint mapped_address)
        {
            return MapCpu(address, ref mapped_address);
        }

        public override bool CpuMapWrite(ushort address, ref uint mapped_address)
        {
            return MapCpu(address, ref mapped_address);
        }

        public override bool PpuMapRead(ushort address, ref uint mapped_address)
        {
            if (address >= 0x0000 && address <= 0x1FFF)
            {
                mapped_address = address;
                return true;
            }

            return false;
        }

        public override bool PpuMapWrite(ushort address, ref uint mapped_address)
        {
            if (address >= 0x0000 && address <= 0x1FFF)
            {
                if (chrBanks == 0)
                {
                    // Treat as RAM
                    mapped_address = address;
                    return true;
                }
            }

            return false;
        }
    }
}
