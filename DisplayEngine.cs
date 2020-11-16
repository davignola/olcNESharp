using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NESharp.Chip;
using olc.wrapper;

namespace NESharp
{
    public class DisplayEngine : PixelGameEngineManaged
    {
        private NesBus nes = new NesBus();
        Dictionary<ushort, string> mapAsm;

        string hex(uint n, byte d)
        {
            return $"{n:X}";
        }

        private void DrawRam(int x, int y, ushort nAddr, int nRows, int nColumns)
        {
            int nRamX = x, nRamY = y;
            for (int row = 0; row < nRows; row++)
            {
                string sOffset = "$" + hex(nAddr, 4) + ":";
                for (int col = 0; col < nColumns; col++)
                {
                    sOffset += " " + hex(nes.Read(nAddr), 2);
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

        // TODO: HORRIBLE CODE
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

        public override bool OnUserCreate()
        {
            var program = "A2 0A 8E 00 00 A2 03 8E 01 00 AC 00 00 A9 00 18 6D 01 00 88 D0 FA 8D 02 00 EA EA EA";

            ushort nOffset = 0x8000;
            foreach (var b in program.Split(' ').Select(s => Convert.ToInt32(s, 16)))
            {
                nes.Ram[nOffset++] = (byte)b;
            }

            // Set Reset Vector
            nes.Ram[0xFFFC] = 0x00;
            nes.Ram[0xFFFD] = 0x80;

            // Dont forget to set IRQ and NMI vectors if you want to play with those

            // Extract dissassembly
            mapAsm = nes.Cpu6502.Disassemble(0x0000, 0xFFFF);

            nes.Cpu6502.Reset();
            return true;
        }

        public override bool OnUserUpdate(float fElapsedTime)
        {
            Clear(PixelColor.DARK_BLUE);

            if (GetKey(KeyManaged.SPACE).bPressed)
            {
                do
                {
                    nes.Cpu6502.Clock();
                }
                while (!nes.Cpu6502.HasCompletedCycle);
            }

            if (GetKey(KeyManaged.R).bPressed)
            {
                nes.Cpu6502.Reset();
            }

            if (GetKey(KeyManaged.I).bPressed)
            {
                nes.Cpu6502.IRQ();
            }

            if (GetKey(KeyManaged.N).bPressed)
            {
                nes.Cpu6502.NMI();
            }

            // Draw Ram Page 0x00		
            DrawRam(2, 2, 0x0000, 16, 16);
            DrawRam(2, 182, 0x8000, 16, 16);
            DrawCpu(448, 2);
            DrawCode(448, 72, 26);


            DrawString(10, 370, "SPACE = Step Instruction    R = RESET    I = IRQ    N = NMI");

            return true;
        }

        public override bool OnUserDestroy()
        {
            return true;
        }
    }
}
