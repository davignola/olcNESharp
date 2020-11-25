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
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using NESharp.Mapper;

namespace NESharp.Components
{
    public class Cartridge
    {
        public const int INES_HEADER_SIZE = 16;


        // Header structure
        public class HeaderINES
        {
            public string Name { get; private set; }
            public byte Prg_rom_chunks { get; private set; }
            public byte Chr_rom_chunks { get; private set; }
            public byte Mapper1 { get; private set; }
            public byte Mapper2 { get; private set; }
            public byte Prg_ram_size { get; private set; }
            public byte Tv_system1 { get; private set; }
            public byte Tv_system2 { get; private set; }
            public string Unused { get; private set; }

            public HeaderINES(byte[] rawHeader)
            {
                Name = Encoding.ASCII.GetString(rawHeader, 0, 4);
                Prg_rom_chunks = rawHeader[4];
                Chr_rom_chunks = rawHeader[5];
                Mapper1 = rawHeader[6];
                Mapper2 = rawHeader[7];
                Prg_ram_size = rawHeader[8];
                Tv_system1 = rawHeader[9];
                Tv_system2 = rawHeader[10];
                Unused = Encoding.ASCII.GetString(rawHeader, 11, 5);
            }
        }

        public enum MIRROR : byte
        {
            HARDWARE,
            HORIZONTAL,
            VERTICAL,
            ONESCREEN_LO,
            ONESCREEN_HI,
        }


        public HeaderINES Header;
        private MIRROR HwMirror = MIRROR.HORIZONTAL;

        public bool IsImageValid { get; set; } = false;

        private byte nMapperID = 0;
        private byte nPRGBanks = 0;
        private byte nCHRBanks = 0;

        private byte[] vPRGMemory;
        private byte[] vCHRMemory;

        private IMapper pMapper;


        public Cartridge(string fileName)
        {
            if (!File.Exists(fileName)) { return; }

            try
            {
                using (var fileReader = File.OpenRead(fileName))
                {
                    if (!fileReader.CanRead) { return; }

                    // Read header data into a buffer
                    var rawHeader = new byte[INES_HEADER_SIZE];
                    fileReader.Read(rawHeader, 0, INES_HEADER_SIZE);

                    // Init the header
                    Header = new HeaderINES(rawHeader);

                    // If a "trainer" exists we just need to read past
                    // it before we get to the good stuff
                    if ((Header.Mapper1 & 0x04) != 0)
                    {
                        fileReader.Seek(512, SeekOrigin.Current);
                    }

                    // Determine Mapper ID
                    nMapperID = (byte)(((Header.Mapper2 >> 4) << 4) | (Header.Mapper1 >> 4));
                    HwMirror = (Header.Mapper1 & 0x01) != 0 ? MIRROR.VERTICAL : MIRROR.HORIZONTAL;

                    // "Discover" File Format
                    byte nFileType = 1;
                    if ((Header.Mapper2 & 0x0C) == 0x08) nFileType = 2;

                    if (nFileType == 0)
                    {

                    }

                    if (nFileType == 1)
                    {
                        nPRGBanks = Header.Prg_rom_chunks;
                        vPRGMemory = new byte[nPRGBanks * 16384];
                        fileReader.Read(vPRGMemory, 0, vPRGMemory.Length);

                        nCHRBanks = Header.Chr_rom_chunks;
                        if (nCHRBanks == 0)
                        {
                            // Create CHR RAM
                            vCHRMemory = new byte[ 8192];
                        }
                        else
                        {
                            // Allocate for ROM
                            vCHRMemory = new byte[nCHRBanks * 8192];
                        }
                        fileReader.Read(vCHRMemory, 0, vCHRMemory.Length);
                    }

                    if (nFileType == 2)
                    {
                        nPRGBanks = (byte)(((Header.Prg_ram_size & 0x07) << 8) | Header.Prg_rom_chunks);
                        vPRGMemory = new byte[nPRGBanks * 16384];
                        fileReader.Read(vPRGMemory, 0, vPRGMemory.Length);

                        nCHRBanks = (byte)(((Header.Prg_ram_size & 0x38) << 8) | Header.Chr_rom_chunks);
                        vCHRMemory = new byte[nCHRBanks * 8192];
                        fileReader.Read(vCHRMemory, 0, vCHRMemory.Length);
                    }

                    // Load appropriate mapper
                    switch (nMapperID)
                    {
                        case 0: pMapper = new Mapper000(nPRGBanks, nCHRBanks); break;
                        case 1: pMapper = new Mapper001(nPRGBanks, nCHRBanks); break;
                        case 2: pMapper = new Mapper002(nPRGBanks, nCHRBanks); break;
                        case 3: pMapper = new Mapper003(nPRGBanks, nCHRBanks); break;
                        case 4: pMapper = new Mapper004(nPRGBanks,nCHRBanks); break;
                        case 66: pMapper = new Mapper066(nPRGBanks, nCHRBanks); break;
                        default: throw new Exception($"Mapper {nMapperID} not implemented");
                    }

                    IsImageValid = true;
                }
            }
            catch (Exception)
            {
                throw;
            }

        }

        public bool CpuRead(ushort address, ref byte data)
        {
            uint mappedAddress = 0;
            if (pMapper.CpuMapRead(address, ref mappedAddress, ref data))
            {
                if (mappedAddress == 0xFFFFFFFF)
                {
                    // Mapper has actually set the data value, for example cartridge based RAM
                    return true;
                }
                else
                {
                    // Mapper has produced an offset into cartridge bank memory
                    data = vPRGMemory[mappedAddress];
                }
                return true;
            }

            return false;
        }
        public bool CpuWrite(ushort address, byte data)
        {
            uint mappedAddress = 0;
            if (pMapper.CpuMapWrite(address, ref mappedAddress, data))
            {
                if (mappedAddress == 0xFFFFFFFF)
                {
                    // Mapper has actually set the data value, for example cartridge based RAM
                    return true;
                }
                else
                {
                    // Mapper has produced an offset into cartridge bank memory
                    vPRGMemory[mappedAddress] = data;
                }
                return true;
            }

            return false;
        }
        public bool PpuRead(ushort address, ref byte data)
        {
            uint mappedAddress = 0;
            if (pMapper.PpuMapRead(address, ref mappedAddress))
            {
                data = vCHRMemory[mappedAddress];
                return true;
            }

            return false;
        }
        public bool PpuWrite(ushort address, byte data)
        {
            uint mappedAddress = 0;
            if (pMapper.PpuMapWrite(address, ref mappedAddress))
            {
                vCHRMemory[mappedAddress] = data;
                return true;
            }

            return false;
        }

        public void Reset()
        {
            // Note: This does not reset the ROM contents,
            // but does reset the mapper.
            pMapper?.Reset();
        }

        public MIRROR GetMirror()
        {
            MIRROR m = pMapper.Mirror;
            if (m == MIRROR.HARDWARE)
            {
                // GetMirror configuration was defined
                // in hardware via soldering
                return HwMirror;
            }
            else
            {
                // GetMirror configuration can be
                // dynamically set via mapper
                return m;
            }
        }

        public IMapper GetMapper()
        {
            return pMapper;
        }
    }
}
