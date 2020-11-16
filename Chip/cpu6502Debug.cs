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
using System.Xml;

namespace NESharp.Chip
{
    public sealed partial class Cpu6502 : IConnectableDevice
    {
        // This is the disassembly function. Its workings are not required for emulation.
        // It is merely a convenience function to turn the binary instruction code into
        // human Readable form. Its included as part of the emulator because it can take
        // advantage of many of the CPUs internal operations to do this.
        public Dictionary<ushort, string> Disassemble(ushort nStart, ushort nStop)
        {
            uint addr = nStart;
            byte value = 0x00, lo = 0x00, hi = 0x00;
            Dictionary<ushort, string> mapLines = new Dictionary<ushort, string>();
            ushort line_addr = 0;

            // Starting at the specified address we Read an instruction
            // byte, which in turn yields information from the Lookup table
            // as to how many additional bytes we need to Read and what the
            // addressing mode is. I need this info to assemble human Readable
            // syntax, which is different depending upon the addressing mode

            // As the instruction is decoded, a std::string is assembled
            // with the Readable output
            while (addr <= nStop)
            {
                line_addr = (ushort)addr;

                // Prefix line with instruction address
                string sInst = $"${addr:X}: ";

                // Read instruction, and get its Readable name
                byte opcode = Bus.Read((ushort)addr);
                addr++;
                sInst += Lookup[opcode].Name + " ";

                // Get oprands from desired locations, and form the
                // instruction based upon its addressing mode. These
                // routines mimmick the actual fetch routine of the
                // 6502 in order to get accurate data as part of the
                // instruction
                if (Lookup[opcode].AddressModeFunc == IMP)
                {
                    sInst += " {IMP}";
                }
                else if (Lookup[opcode].AddressModeFunc == IMM)
                {
                    value = Bus.Read((ushort)addr); addr++;
                    sInst += $"#${value:X} {{IMM}}";
                }
                else if (Lookup[opcode].AddressModeFunc == ZP0)
                {
                    lo = Bus.Read((ushort)addr); addr++;
                    hi = 0x00;
                    sInst += $"${lo:X} {{ZP0}}";
                }
                else if (Lookup[opcode].AddressModeFunc == ZPX)
                {
                    lo = Bus.Read((ushort)addr); addr++;
                    hi = 0x00;
                    sInst += $"${lo:X}, X {{ZPX}}";
                }
                else if (Lookup[opcode].AddressModeFunc == ZPY)
                {
                    lo = Bus.Read((ushort)addr); addr++;
                    hi = 0x00;
                    sInst += $"${lo:X}, Y {{ZPY}}";
                }
                else if (Lookup[opcode].AddressModeFunc == IZX)
                {
                    lo = Bus.Read((ushort)addr); addr++;
                    hi = 0x00;
                    sInst += $"(${lo:X}, X) {{IZX}}";
                }
                else if (Lookup[opcode].AddressModeFunc == IZY)
                {
                    lo = Bus.Read((ushort)addr); addr++;
                    hi = 0x00;
                    sInst += $"(${lo:X}), Y {{IZY}}";
                }
                else if (Lookup[opcode].AddressModeFunc == ABS)
                {
                    lo = Bus.Read((ushort)addr); addr++;
                    hi = Bus.Read((ushort)addr); addr++;
                    sInst += $"${(hi << 8) | lo:X} {{ABS}}";
                }
                else if (Lookup[opcode].AddressModeFunc == ABX)
                {
                    lo = Bus.Read((ushort)addr); addr++;
                    hi = Bus.Read((ushort)addr); addr++;
                    sInst += $"${(hi << 8) | lo:X}, X {{ABX}}";
                }
                else if (Lookup[opcode].AddressModeFunc == ABY)
                {
                    lo = Bus.Read((ushort)addr); addr++;
                    hi = Bus.Read((ushort)addr); addr++;
                    sInst += $"${(hi << 8) | lo:X}, Y {{ABY}}";
                }
                else if (Lookup[opcode].AddressModeFunc == IND)
                {
                    lo = Bus.Read((ushort)addr); addr++;
                    hi = Bus.Read((ushort)addr); addr++;
                    sInst += $"(${(hi << 8) | lo:X}) {{IND}}";
                }
                else if (Lookup[opcode].AddressModeFunc == REL)
                {
                    value = Bus.Read((ushort)addr); addr++;
                    sInst += $"${value:X} [${addr + value:X}] {{REL}}";
                }

                // Add the formed string to a std::map, using the instruction's
                // address as the key. This makes it convenient to look for later
                // as the instructions are variable in length, so a straight up
                // incremental index is not sufficient.
                mapLines[line_addr] = sInst;
            }

            return mapLines;
        }
    }
}
