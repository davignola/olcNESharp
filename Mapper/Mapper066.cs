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
    public class Mapper066 : MapperBase
    {

        byte nCHRBankSelect = 0x00;
        byte nPRGBankSelect = 0x00;


        public Mapper066(byte prgBanks, byte chrBanks) : base(prgBanks, chrBanks)
        {
        }


        public override bool CpuMapRead(ushort address, ref uint mapped_address, ref byte data)
        {
            if (address >= 0x8000 && address <= 0xFFFF)
            {
                mapped_address = (uint)(nPRGBankSelect * 0x8000 + (address & 0x7FFF));
                return true;
            }
            else
                return false;
        }

        public override bool CpuMapWrite(ushort address, ref uint mapped_address, byte data)
        {
            if (address >= 0x8000 && address <= 0xFFFF)
            {
                nCHRBankSelect = (byte)(data & 0x03);
                nPRGBankSelect = (byte)((data & 0x30) >> 4);
            }

            // Mapper has handled write, but do not update ROMs
            return false;
        }

        public override bool PpuMapRead(ushort address, ref uint mapped_address)
        {
            if (address < 0x2000)
            {
                mapped_address = (uint)(nCHRBankSelect * 0x2000 + address);
                return true;
            }
            else
                return false;
        }

        public override bool PpuMapWrite(ushort address, ref uint mapped_address)
        {
            return false;
        }

        public override void Reset()
        {
            nCHRBankSelect = 0;
            nPRGBankSelect = 0;
        }


    }
}
