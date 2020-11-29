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
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NESharp.Components;
using Nito.Collections;
using olc.managed;

namespace NESharp
{
    public class DisplayEngine : PixelGameEngineManaged
    {
        private Cartridge cartridge;
        private NesBus nes = new NesBus();
        Dictionary<ushort, string> mapAsm;
        private bool emulationRun = false;
        private float fResidualTime = 0.0f;
        private byte nSelectedPalette = 0x00;
        private bool breakFrameCycle = false;

        Deque<ushort>[] audio = new Deque<ushort>[4];
        float fAccumulatedTime = 0.0f;

        private ViewMode currentvireViewMode = ViewMode.Graphics;

        public enum ViewMode : byte
        {
            Code = 0,
            Graphics = 1,
            Audio = 2
        }

        string hex(uint n, byte d)
        {
            return $"{n:X2}";
        }

        void DrawAudio(int channel, int x, int y)
        {
            FillRect(x, y, 120, 120, PixelColor.BLACK);
            int i = 0;
            foreach (var s in audio[channel])
            {
                Draw(x + i, y + (s >> (channel == 2 ? 5 : 4)), PixelColor.YELLOW);
                i++;
            }
        }

        private void DrawRam(int x, int y, ushort nAddr, int nRows, int nColumns)
        {
            int nRamX = x, nRamY = y;
            for (int row = 0; row < nRows; row++)
            {
                string sOffset = "$" + hex(nAddr, 4) + ":";
                for (int col = 0; col < nColumns; col++)
                {
                    sOffset += " " + hex(nes.CpuRead(nAddr, true), 2);
                    nAddr += 1;
                }
                DrawString(nRamX, nRamY, sOffset);
                nRamY += 10;
            }
        }

        void DrawCpu(int x, int y)
        {
            DrawString(x, y, "STATUS:", PixelColor.WHITE);
            DrawString(x + 64, y, "N", nes.Cpu6502.Status.HasFlag(Cpu6502.FLAGS6502.N) ? PixelColor.GREEN : PixelColor.RED);
            DrawString(x + 80, y, "V", nes.Cpu6502.Status.HasFlag(Cpu6502.FLAGS6502.V) ? PixelColor.GREEN : PixelColor.RED);
            DrawString(x + 96, y, "-", nes.Cpu6502.Status.HasFlag(Cpu6502.FLAGS6502.U) ? PixelColor.GREEN : PixelColor.RED);
            DrawString(x + 112, y, "B", nes.Cpu6502.Status.HasFlag(Cpu6502.FLAGS6502.B) ? PixelColor.GREEN : PixelColor.RED);
            DrawString(x + 128, y, "D", nes.Cpu6502.Status.HasFlag(Cpu6502.FLAGS6502.D) ? PixelColor.GREEN : PixelColor.RED);
            DrawString(x + 144, y, "I", nes.Cpu6502.Status.HasFlag(Cpu6502.FLAGS6502.I) ? PixelColor.GREEN : PixelColor.RED);
            DrawString(x + 160, y, "Z", nes.Cpu6502.Status.HasFlag(Cpu6502.FLAGS6502.Z) ? PixelColor.GREEN : PixelColor.RED);
            DrawString(x + 178, y, "C", nes.Cpu6502.Status.HasFlag(Cpu6502.FLAGS6502.C) ? PixelColor.GREEN : PixelColor.RED);
            DrawString(x, y + 10, "PC: $" + hex(nes.Cpu6502.Pc, 4));
            DrawString(x, y + 20, "A: $" + hex(nes.Cpu6502.A, 2) + "  [" + nes.Cpu6502.A + "]");
            DrawString(x, y + 30, "X: $" + hex(nes.Cpu6502.X, 2) + "  [" + nes.Cpu6502.X + "]");
            DrawString(x, y + 40, "Y: $" + hex(nes.Cpu6502.Y, 2) + "  [" + nes.Cpu6502.Y + "]");
            DrawString(x, y + 50, "Stack P: $" + hex(nes.Cpu6502.StkPtr, 4));
        }

        // TODO: HORRIBLE CODE, that did not port well, I will rewrite this in future parts
        void DrawCode(int x, int y, int nLines)
        {
            var lastIdx = mapAsm.Last().Key;
            if (!mapAsm.ContainsKey(nes.Cpu6502.Pc)) { return; }
            var it_a = mapAsm.Single(s => s.Key == nes.Cpu6502.Pc);
            int nLineY = (nLines >> 1) * 10 + y;
            if (it_a.Key != lastIdx)
            {
                DrawString(x, nLineY, it_a.Value, PixelColor.CYAN);

                foreach (var asm in mapAsm.Skip(mapAsm.Keys.ToList().IndexOf(it_a.Key) + 1).Take(10))
                {
                    nLineY += 10;
                    DrawString(x, nLineY, asm.Value);

                }

            }

            nLineY = (nLines >> 1) * 10 + y;
            if (it_a.Key != lastIdx)
            {
                foreach (var asm in mapAsm.Reverse().Skip(mapAsm.Keys.Reverse().ToList().IndexOf(it_a.Key) + 1).Take(10))
                {
                    nLineY -= 10;
                    DrawString(x, nLineY, asm.Value);
                }
            }
        }

        private void DrawGraphicsData()
        {
            if (nes.Ppu2C02.OAM != null)
            {
                // Draw OAM Contents (first 26 out of 64) ======================================
                for (int i = 0; i < 26; i++)
                {
                    string s = $"{i:X2}: ({nes.Ppu2C02.OAM[i * 4 + 3]}"
                                    + $", {nes.Ppu2C02.OAM[i * 4 + 0]}) "
                                    + $"ID: {nes.Ppu2C02.OAM[i * 4 + 1]:X2}"
                                    + $" AT: {nes.Ppu2C02.OAM[i * 4 + 2]:X2}";
                    DrawString(516, 72 + i * 10, s);
                }
            }

            // Draw Palettes & Pattern Tables ==============================================
            const int nSwatchSize = 6;
            for (byte p = 0; p < 8; p++) // For each palette
                for (byte s = 0; s < 4; s++) // For each index
                    FillRect(516 + p * (nSwatchSize * 5) + s * nSwatchSize, 340, nSwatchSize, nSwatchSize, nes.Ppu2C02.GetColourFromPaletteRam(p, s));

            // Draw selection reticule around selected palette
            DrawRect(516 + nSelectedPalette * (nSwatchSize * 5) - 1, 339, (nSwatchSize * 4), nSwatchSize, PixelColor.WHITE);

            // Generate Pattern Tables
            DrawSprite(516, 348, nes.Ppu2C02.GetPatternTable(0, nSelectedPalette));
            DrawSprite(648, 348, nes.Ppu2C02.GetPatternTable(1, nSelectedPalette));
        }

        // This function is called by the underlying sound hardware
        // which runs in a different thread. It is automatically
        // synchronised with the sample rate of the sound card, and
        // expects a single "sample" to be returned, whcih ultimately
        // makes its way to your speakers, and then your ears, for that
        // lovely 8-bit bliss... but, that means we've some thread
        // handling to deal with, since we want both the PGE thread
        // and the sound system thread to interact with the emulator.

        static DisplayEngine pInstance; // Static variable that will hold a pointer to "this"

        static float SoundOut(int nChannel, float fGlobalTime, float fTimeStep)
        {
            if (nChannel == 0)
            {
                while (!pInstance.nes.Clock()) { };
                return (float)pInstance.nes.dAudioSample;
            }
            else
                return 0.0f;
        }

        public override bool OnUserCreate()
        {
            // Load the cartridge
            cartridge = new Cartridge("../../../../TestRoms/mario.nes");
            if (!cartridge.IsImageValid) { return false; }

            // Insert into NES
            nes.InsertCartridge(cartridge);

            // Extract dissassembly
            mapAsm = nes.Cpu6502.Disassemble(0x8000, 0xFFFF);

            // Init channels
            for (var i = 0; i < audio.Length; i++)
            {
                audio[i] = new Deque<ushort>();
                for (int j = 0; j < 120; j++)
                    audio[i].AddToBack(0);
            }

            nes.Reset();

            // Initialise PGEX sound system, and give it a function to 
            // call which returns a sound sample on demand
            pInstance = this;
            nes.SetSampleFrequency(44100);
            olcPGEXSoundManaged.InitialiseAudio(44100, 1, 8, 512);
            olcPGEXSoundManaged.SetUserSynthFunction(SoundOut);
            return true;
        }


        public override bool OnUserUpdate(float fElapsedTime)
        {
            return EmulatorUpdateWithAudio(fElapsedTime);
        }

        /// <summary>
        /// This performs an emulation update but synced to audio, so it cant
        /// perform stepping through code or frames. Essentially, it runs
        /// the emulation in real time now, so only accepts "controller" input
        /// and updates the display
        /// </summary>
        /// <param name="fElapsedTime"></param>
        /// <returns></returns>
        private bool EmulatorUpdateWithAudio(float fElapsedTime)
        {
            // Sample audio channel output roughly once per frame
            fAccumulatedTime += fElapsedTime;
            if (fAccumulatedTime >= 1.0f / 60.0f)
            {
                fAccumulatedTime -= (1.0f / 60.0f);
                audio[0].RemoveFromFront();
                audio[0].AddToBack(nes.Apu2A03.pulse1_visual);
                audio[1].RemoveFromFront();
                audio[1].AddToBack(nes.Apu2A03.pulse2_visual);
                audio[2].RemoveFromFront();
                audio[2].AddToBack(nes.Apu2A03.noise_visual);
            }

            Clear(PixelColor.DARK_BLUE);

            // Sneaky peek of controller input in next video! ;P
            nes.Controller[0] = 0x00;
            nes.Controller[0] |= GetKey(KeyManaged.X).bHeld() ? 0x80 : 0x00;
            nes.Controller[0] |= GetKey(KeyManaged.Z).bHeld() ? 0x40 : 0x00;
            nes.Controller[0] |= GetKey(KeyManaged.A).bHeld() ? 0x20 : 0x00;
            nes.Controller[0] |= GetKey(KeyManaged.S).bHeld() ? 0x10 : 0x00;
            nes.Controller[0] |= GetKey(KeyManaged.UP).bHeld() ? 0x08 : 0x00;
            nes.Controller[0] |= GetKey(KeyManaged.DOWN).bHeld() ? 0x04 : 0x00;
            nes.Controller[0] |= GetKey(KeyManaged.LEFT).bHeld() ? 0x02 : 0x00;
            nes.Controller[0] |= GetKey(KeyManaged.RIGHT).bHeld() ? 0x01 : 0x00;

            if (GetKey(KeyManaged.R).bPressed()) nes.Reset();
            // Hard reset (Power cycle)
            if (GetKey(KeyManaged.H).bPressed()) nes.Reset(true);
            if (GetKey(KeyManaged.P).bPressed())
            {
                nSelectedPalette++;
                nSelectedPalette &= 0x07;
            }

            if (GetKey(KeyManaged.T).bPressed())
            {
                // enter nestest in no graphics mode
                nes.Reset(true);
                nes.Cpu6502.DebugEnabled = true;
                nes.Cpu6502.Pc = 0xC000;
                nes.Cpu6502.DebugBreakPc = 0xC6BC;
                nes.Cpu6502.DebugPcHitCallback = () =>
                {
                    emulationRun = false;
                    breakFrameCycle = true;
                };
            }

            // Cycle view mode
            if (GetKey(KeyManaged.V).bPressed())
            {
                if (currentvireViewMode == ViewMode.Audio)
                {
                    currentvireViewMode = ViewMode.Code;
                }
                else
                {
                    currentvireViewMode++;
                }
            }

            DrawCpu(516, 2);

            if (nes.Cpu6502.DebugEnabled)
            {

                // Draw Ram Page 0x00		
                DrawRam(2, 2, 0x0000, 16, 16);
                DrawRam(2, 182, 0x8000, 16, 16);
            }
            else
            {
                // Draw rendered output ========================================================
                DrawSprite(0, 0, nes.Ppu2C02.GetScreen(), 2);
            }

            switch (currentvireViewMode)
            {
                default:
                case ViewMode.Code:
                    DrawCode(516, 72, 26);
                    break;
                case ViewMode.Graphics:
                    DrawGraphicsData();
                    break;
                case ViewMode.Audio:
                    // Draw AUDIO Channels
                    DrawAudio(0, 520, 72);
                    DrawAudio(1, 644, 72);
                    DrawAudio(2, 520, 196);
                    DrawAudio(3, 644, 196);
                    break;
            }
            
            return true;
        }

        /// <summary>
        ///  We must play nicely now with the sound hardware, so unload
        /// it when the application terminates
        /// </summary>
        /// <returns></returns>
        public override bool OnUserDestroy()
        {
            olcPGEXSoundManaged.DestroyAudio();
            return true;
        }
    }
}
