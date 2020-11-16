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
using System.Xml;

namespace NESharp.Chip
{
    public sealed partial class Cpu6502 : IConnectableDevice
    {

        #region Address mode functions
        ///////////////////////////////////////////////////////////////////////////////
        // ADDRESSING MODES

        // The 6502 can address between 0x0000 - 0xFFFF. The high byte is often referred
        // to as the "page", and the low byte is the offset into that page. This implies
        // there are 256 pages, each containing 256 bytes.
        //
        // Several addressing modes have the potential to require an additional clock
        // cycle if they cross a page boundary. This is combined with several instructions
        // that enable this additional clock cycle. So each addressing function returns
        // a flag saying it has potential, as does each instruction. If both instruction
        // and address function return 1, then an additional clock cycle is required.

        /// <summary>
        /// Address Mode: Implied
        /// There is no additional data required for this instruction. The instruction
        /// does something very simple like like sets a status bit. However, we will
        /// target the accumulator, for instructions like PHA
        /// </summary>
        /// <returns></returns>
        public byte IMP()
        {
            fetched = A;
            return 0;
        }

        /// <summary>
        /// Address Mode: Immediate
        /// The instruction expects the next byte to be used as a value, so we'll prep
        /// the read address to point to the next byte
        /// </summary>
        /// <returns></returns>
        public byte IMM()
        {
            addr_abs = Pc++;
            return 0;
        }

        /// <summary>
        /// Address Mode: Zero Page
        /// To save program bytes, zero page addressing allows you to absolutely address
        /// a location in first 0xFF bytes of address range. Clearly this only requires
        /// one byte instead of the usual two.
        /// </summary>
        /// <returns></returns>
        public byte ZP0()
        {
            addr_abs = Read(Pc);
            Pc++;
            addr_abs &= 0x00FF;
            return 0;
        }

        /// <summary>
        /// Address Mode: Zero Page with X Offset
        /// Fundamentally the same as Zero Page addressing, but the contents of the X Register
        /// is added to the supplied single byte address. This is useful for iterating through
        /// ranges within the first page.
        /// </summary>
        /// <returns></returns>
        public byte ZPX()
        {
            addr_abs = (ushort)(ReadPc() + X);
            addr_abs &= 0x00FF;
            return 0;
        }

        /// <summary>
        /// Address Mode: Zero Page with Y Offset
        /// Same as above but uses Y Register for offset
        /// </summary>
        /// <returns></returns>
        public byte ZPY()
        {
            addr_abs = (ushort)(ReadPc() + Y);
            addr_abs &= 0x00FF;
            return 0;
        }

        /// <summary>
        /// Address Mode: Relative
        /// This address mode is exclusive to branch instructions. The address
        /// must reside within -128 to +127 of the branch instruction, i.e.
        /// you cant directly branch to any address in the addressable range.
        /// </summary>
        /// <returns></returns>
        public byte REL()
        {
            addr_rel = ReadPc();
            if ((addr_rel & 0x80) != 0)
            {
                addr_rel |= 0xFF00;
            }
            return 0;
        }

        /// <summary>
        /// Address Mode: Absolute 
        /// A full 16-bit address is loaded and used
        /// </summary>
        /// <returns></returns>
        public byte ABS()
        {
            addr_abs = ReadPcAsAddress();
            return 0;
        }

        /// <summary>
        /// Address Mode: Absolute with X Offset
        /// Fundamentally the same as absolute addressing, but the contents of the X Register
        /// is added to the supplied two byte address. If the resulting address changes
        /// the page, an additional clock cycle is required
        /// </summary>
        /// <returns></returns>
        public byte ABX()
        {
            ushort beforeOffset = ReadPcAsAddress();
            addr_abs = (ushort)(beforeOffset + X);

            if ((addr_abs & 0xFF00) != (beforeOffset & 0xFF00))
            {
                return 1;
            }
            else
            {
                return 0;
            }
        }

        /// <summary>
        /// Address Mode: Absolute with Y Offset
        /// Fundamentally the same as absolute addressing, but the contents of the Y Register
        /// is added to the supplied two byte address. If the resulting address changes
        /// the page, an additional clock cycle is required
        /// </summary>
        /// <returns></returns>
        public byte ABY()
        {
            ushort beforeOffset = ReadPcAsAddress();
            addr_abs = (ushort)(beforeOffset + Y);

            if ((addr_abs & 0xFF00) != (beforeOffset & 0xFF00))
            {
                return 1;
            }
            else
            {
                return 0;
            }
        }

        /// <summary>
        /// Address Mode: Indirect
        /// The supplied 16-bit address is read to get the actual 16-bit address. This is
        /// instruction is unusual in that it has a bug in the hardware! To emulate its
        /// function accurately, we also need to emulate this bug. If the low byte of the
        /// supplied address is 0xFF, then to read the high byte of the actual address
        /// we need to cross a page boundary. This doesnt actually work on the chip as 
        /// designed, instead it wraps back around in the same page, yielding an 
        /// invalid actual address
        /// </summary>
        /// <returns></returns>
        public byte IND()
        {
            ushort pointer = ReadPcAsAddress();

            if ((pointer & 0x00FF) == 0x00FF) // Simulate page boundary hardware bug
            {
                addr_abs = (ushort)((Read((ushort)(pointer & 0xFF00)) << 8) | Read((ushort)(pointer + 0)));
            }
            else // Behave normally
            {
                addr_abs = (ushort)((Read((ushort)(pointer + 1)) << 8) | Read((ushort)(pointer + 0)));
            }

            return 0;
        }

        /// <summary>
        /// Address Mode: Indirect X
        /// The supplied 8-bit address is offset by X Register to index
        /// a location in page 0x00. The actual 16-bit address is read 
        /// from this location
        /// </summary>
        /// <returns></returns>
        public byte IZX()
        {
            ushort t = ReadPc();
            addr_abs = ReadAsAddress((ushort)(t + X));
            return 0;
        }

        /// <summary>
        /// Address Mode: Indirect Y
        /// The supplied 8-bit address indexes a location in page 0x00. From 
        /// here the actual 16-bit address is read, and the contents of
        /// Y Register is added to it to offset it. If the offset causes a
        /// change in page then an additional clock cycle is required.
        /// </summary>
        /// <returns></returns>
        public byte IZY()
        {
            ushort t = ReadPc();
            addr_abs = ReadAsAddress((ushort)(t + Y));
            return 0;
        }

        #endregion

        /// <summary>
        /// This function sources the data used by the instruction into 
        /// a convenient numeric variable. Some instructions dont have to 
        /// fetch data as the source is implied by the instruction. For example
        /// "INX" increments the X register. There is no additional data
        /// required. For all other addressing modes, the data resides at 
        /// the location held within addr_abs, so it is read from there. 
        /// Immediate adress mode exploits this slightly, as that has
        /// set addr_abs = pc + 1, so it fetches the data from the
        /// next byte for example "LDA $FF" just loads the accumulator with
        /// 256, i.e. no far reaching memory fetch is required. "fetched"
        /// is a variable global to the CPU, and is set by calling this 
        /// function. It also returns it for convenience.
        /// </summary>
        /// <returns></returns>
        public byte Fetch()
        {
            if (Lookup[opcode].AddressModeFunc != IMP)
            {
                fetched = Read(addr_abs);
            }
            return fetched;
        }

        #region Opcode instructions

        /// <summary>
        /// Add function with carry and overflow flag awareness
        /// (TODODA: put in the whole doc)
        /// </summary>
        /// <returns></returns>
        public byte ADC()
        {
            // Grab the data that we are adding to the accumulator
            Fetch();

            // Add is performed in 16-bit domain for emulation to capture any
            // carry bit, which will exist in bit 8 of the 16-bit word
            temp = (ushort)(A + fetched + (Status.HasFlag(FLAGS6502.C) ? 1 : 0));

            // The carry flag out exists in the high byte bit 0
            SetFlag(FLAGS6502.C, temp > 255);

            // The Zero flag is set if the result is 0
            SetFlag(FLAGS6502.Z, (temp & 0x00FF) == 0);

            // The signed Overflow flag is set based on all that up there! :D
            SetFlag(FLAGS6502.V, ((~(A ^ fetched) & (A ^ temp)) & 0x0080) != 0);

            // The negative flag is set to the most significant bit of the result
            SetFlag(FLAGS6502.N, (temp & 0x80) != 0);

            // Load the result into the accumulator (it's 8-bit dont forget!)
            A = (byte)(temp & 0x00FF);

            // This instruction has the potential to require an additional clock cycle
            return 1;
        }

        /// <summary>
        /// Instruction: Subtraction with Borrow In
        /// Function:    A = A - M - (1 - C)
        /// Flags Out:   C, V, N, Z
        ///
        /// Explanation:
        /// Given the explanation for ADC above, we can reorganise our data
        /// to use the same computation for addition, for subtraction by multiplying
        /// the data by -1, i.e. make it negative
        ///
        /// A = A - M - (1 - C)  ->  A = A + -1 * (M - (1 - C))  ->  A = A + (-M + 1 + C)
        ///
        /// To make a signed positive number negative, we can invert the bits and add 1
        /// (OK, I lied, a little bit of 1 and 2s complement :P)
        ///
        ///  5 = 00000101
        /// -5 = 11111010 + 00000001 = 11111011 (or 251 in our 0 to 255 range)
        ///
        /// The range is actually unimportant, because if I take the value 15, and add 251
        /// to it, given we wrap around at 256, the result is 10, so it has effectively 
        /// subtracted 5, which was the original intention. (15 + 251) % 256 = 10
        ///
        /// Note that the equation above used (1-C), but this got converted to + 1 + C.
        /// This means we already have the +1, so all we need to do is invert the bits
        /// of M, the data(!) therfore we can simply add, exactly the same way we did 
        /// before.
        /// </summary>
        /// <returns></returns>
        public byte SBC()
        {
            Fetch();

            // Operating in 16-bit domain to capture carry out

            // We can invert the bottom 8 bits with bitwise xor
            ushort value = (ushort)(fetched ^ 0x00FF);

            // Notice this is exactly the same as addition from here!
            temp = (ushort)(A + fetched + (Status.HasFlag(FLAGS6502.C) ? 1 : 0));
            SetFlag(FLAGS6502.C, temp > 255);
            SetFlag(FLAGS6502.Z, (temp & 0x00FF) == 0);
            SetFlag(FLAGS6502.V, ((~(A ^ fetched) & (A ^ temp)) & 0x0080) != 0);
            SetFlag(FLAGS6502.N, (temp & 0x80) != 0);
            A = (byte)(temp & 0x00FF);
            return 1;
        }

        /// <summary>
        /// Instruction: Bitwise Logic AND
        /// Function:    A = A &amp; M
        /// Flags Out:   N, Z
        /// </summary>
        /// <returns></returns>
        public byte AND()
        {
            Fetch();
            A = (byte)(A & fetched);
            SetFlag(FLAGS6502.Z, A == 0x00);
            SetFlag(FLAGS6502.N, (A & 0x80) != 0);
            return 1;
        }

        /// <summary>
        /// Instruction: Arithmetic Shift Left
        /// Function:    A = C <- (A << 1) <- 0
        /// Flags Out:   N, Z, C
        /// </summary>
        /// <returns></returns>
        public byte ASL()
        {
            Fetch();
            temp = (ushort)(fetched << 1);
            SetFlag(FLAGS6502.C, (temp & 0xFF00) > 0);
            SetFlag(FLAGS6502.Z, (temp & 0x00FF) == 0x00);
            SetFlag(FLAGS6502.N, (temp & 0x80) != 0);
            if (Lookup[opcode].AddressModeFunc == IMP)
                A = (byte)(temp & 0x00FF);
            else
                Write(addr_abs, (byte)(temp & 0x00FF));
            return 0;
        }

        /// <summary>
        /// Instruction: Branch if Carry Clear
        /// Function:    if(C == 0) pc = address 
        /// </summary>
        /// <returns></returns>
        public byte BCC()
        {
            if (!Status.HasFlag(FLAGS6502.C))
            {
                cycles++;
                addr_abs = (ushort)(Pc + addr_rel);

                if ((addr_abs & 0xFF00) != (Pc & 0xFF00))
                {
                    cycles++;
                }

                Pc = addr_abs;
            }
            return 0;
        }

        /// <summary>
        /// Instruction: Branch if Carry Set
        /// Function:    if(C == 1) pc = address
        /// </summary>
        /// <returns></returns>
        public byte BCS()
        {
            if (Status.HasFlag(FLAGS6502.C))
            {
                cycles++;
                addr_abs = (ushort)(Pc + addr_rel);

                if ((addr_abs & 0xFF00) != (Pc & 0xFF00))
                {
                    cycles++;
                }

                Pc = addr_abs;
            }
            return 0;
        }

        /// <summary>
        /// Instruction: Branch if Equal
        /// Function:    if(Z == 1) pc = address
        /// </summary>
        /// <returns></returns>
        public byte BEQ()
        {
            if (Status.HasFlag(FLAGS6502.Z))
            {
                cycles++;
                addr_abs = (ushort)(Pc + addr_rel);

                if ((addr_abs & 0xFF00) != (Pc & 0xFF00))
                {
                    cycles++;
                }

                Pc = addr_abs;
            }
            return 0;
        }

        public byte BIT()
        {
            Fetch();
            temp = (ushort)(A & fetched);
            SetFlag(FLAGS6502.Z, (temp & 0x00FF) == 0x00);
            SetFlag(FLAGS6502.N, (fetched & (1 << 7)) != 0);
            SetFlag(FLAGS6502.V, (fetched & (1 << 6)) != 0);
            return 0;
        }

        /// <summary>
        /// Instruction: Branch if Negative
        /// Function:    if(N == 1) pc = address
        /// </summary>
        /// <returns></returns>
        public byte BMI()
        {
            if (Status.HasFlag(FLAGS6502.N))
            {
                cycles++;
                addr_abs = (ushort)(Pc + addr_rel);

                if ((addr_abs & 0xFF00) != (Pc & 0xFF00))
                {
                    cycles++;
                }

                Pc = addr_abs;
            }
            return 0;
        }

        /// <summary>
        /// Instruction: Branch if Not Equal
        /// Function:    if(Z == 0) pc = address
        /// </summary>
        /// <returns></returns>
        public byte BNE()
        {
            if (!Status.HasFlag(FLAGS6502.Z))
            {
                cycles++;
                addr_abs = (ushort)(Pc + addr_rel);

                if ((addr_abs & 0xFF00) != (Pc & 0xFF00))
                {
                    cycles++;
                }

                Pc = addr_abs;
            }
            return 0;
        }

        /// <summary>
        /// Instruction: Branch if Positive
        /// Function:    if(N == 0) pc = address
        /// </summary>
        /// <returns></returns>
        public byte BPL()
        {
            if (!Status.HasFlag(FLAGS6502.N))
            {
                cycles++;
                addr_abs = (ushort)(Pc + addr_rel);

                if ((addr_abs & 0xFF00) != (Pc & 0xFF00))
                {
                    cycles++;
                }

                Pc = addr_abs;
            }
            return 0;
        }

        /// <summary>
        /// Instruction: Break
        /// Function:    Program Sourced Interrupt
        /// </summary>
        /// <returns></returns>
        public byte BRK()
        {
            Pc++;

            SetFlag(FLAGS6502.I, true);
            PushPcOnStack();

            SetFlag(FLAGS6502.B, true);
            PushStack((byte)Status);
            SetFlag(FLAGS6502.B, false);

            Pc = ReadAsAddress(IRQ_PC_START_ADDRESS);
            return 0;
        }

        /// <summary>
        /// Instruction: Branch if Overflow Clear
        /// Function:    if(V == 0) pc = address
        /// </summary>
        /// <returns></returns>
        public byte BVC()
        {
            if (!Status.HasFlag(FLAGS6502.V))
            {
                cycles++;
                addr_abs = (ushort)(Pc + addr_rel);

                if ((addr_abs & 0xFF00) != (Pc & 0xFF00))
                {
                    cycles++;
                }

                Pc = addr_abs;
            }
            return 0;
        }

        /// <summary>
        /// Instruction: Branch if Overflow Set
        /// Function:    if(V == 1) pc = address
        /// </summary>
        /// <returns></returns>
        public byte BVS()
        {
            if (Status.HasFlag(FLAGS6502.V))
            {
                cycles++;
                addr_abs = (ushort)(Pc + addr_rel);

                if ((addr_abs & 0xFF00) != (Pc & 0xFF00))
                {
                    cycles++;
                }

                Pc = addr_abs;
            }
            return 0;
        }

        /// <summary>
        /// Instruction: Clear Carry Flag
        /// Function:    C = 0
        /// </summary>
        /// <returns></returns>
        public byte CLC()
        {
            SetFlag(FLAGS6502.C, false);
            return 0;
        }


        /// <summary>
        /// Instruction: Clear Decimal Flag
        /// Function:    D = 0
        /// </summary>
        /// <returns></returns>
        public byte CLD()
        {
            SetFlag(FLAGS6502.D, false);
            return 0;
        }

        /// <summary>
        /// Instruction: Disable Interrupts / Clear Interrupt Flag
        /// Function:    I = 0
        /// </summary>
        /// <returns></returns>
        public byte CLI()
        {
            SetFlag(FLAGS6502.I, false);
            return 0;
        }

        /// <summary>
        /// Instruction: Clear Overflow Flag
        /// Function:    V = 0
        /// </summary>
        /// <returns></returns>
        public byte CLV()
        {
            SetFlag(FLAGS6502.V, false);
            return 0;
        }

        /// <summary>
        /// Instruction: Compare Accumulator
        /// Function:    C <- A >= M      Z <- (A - M) == 0
        /// Flags Out:   N, C, Z
        /// </summary>
        /// <returns></returns>
        public byte CMP()
        {
            Fetch();
            temp = (ushort)(A & fetched);
            SetFlag(FLAGS6502.C, A >= fetched);
            SetFlag(FLAGS6502.Z, (temp & 0x00FF) == 0);
            SetFlag(FLAGS6502.N, (temp & 0x0080) != 0);
            return 1;
        }

        /// <summary>
        /// Instruction: Compare X Register
        /// Function:    C <- X >= M      Z <- (X - M) == 0
        /// Flags Out:   N, C, Z
        /// </summary>
        /// <returns></returns>
        public byte CPX()
        {
            Fetch();
            temp = (ushort)(X & fetched);
            SetFlag(FLAGS6502.C, X >= fetched);
            SetFlag(FLAGS6502.Z, (temp & 0x00FF) == 0);
            SetFlag(FLAGS6502.N, (temp & 0x0080) != 0);
            return 1;
        }

        /// <summary>
        /// Instruction: Compare Y Register
        /// Function:    C <- Y >= M      Z <- (Y - M) == 0
        /// Flags Out:   N, C, Z
        /// </summary>
        /// <returns></returns>
        public byte CPY()
        {
            Fetch();
            temp = (ushort)(Y & fetched);
            SetFlag(FLAGS6502.C, Y >= fetched);
            SetFlag(FLAGS6502.Z, (temp & 0x00FF) == 0);
            SetFlag(FLAGS6502.N, (temp & 0x0080) != 0);
            return 1;
        }

        /// <summary>
        /// Instruction: Decrement Value at Memory Location
        /// Function:    M = M - 1
        /// Flags Out:   N, Z
        /// </summary>
        /// <returns></returns>
        public byte DEC()
        {
            Fetch();
            temp = (ushort)(fetched - 1);
            Write(addr_abs, (byte)(temp & 0x00FF));
            SetFlag(FLAGS6502.Z, (temp & 0x00FF) == 0);
            SetFlag(FLAGS6502.N, (temp & 0x0080) != 0);
            return 0;
        }

        /// <summary>
        /// Instruction: Decrement X Register
        /// Function:    X = X - 1
        /// Flags Out:   N, Z
        /// </summary>
        /// <returns></returns>
        public byte DEX()
        {
            X--;
            SetFlag(FLAGS6502.Z, (X & 0x00FF) == 0);
            SetFlag(FLAGS6502.N, (X & 0x0080) != 0);
            return 0;
        }

        /// <summary>
        /// Instruction: Decrement Y Register
        /// Function:    Y = Y - 1
        /// Flags Out:   N, Z
        /// </summary>
        /// <returns></returns>
        public byte DEY()
        {
            Y--;
            SetFlag(FLAGS6502.Z, (Y & 0x00FF) == 0);
            SetFlag(FLAGS6502.N, (Y & 0x0080) != 0);
            return 0;
        }

        /// <summary>
        /// Instruction: Bitwise Logic XOR
        /// Function:    A = A xor M
        /// Flags Out:   N, Z
        /// </summary>
        /// <returns></returns>
        public byte EOR()
        {
            Fetch();
            A = (byte)(A ^ fetched);
            SetFlag(FLAGS6502.Z, (A & 0x00FF) == 0);
            SetFlag(FLAGS6502.N, (A & 0x0080) != 0);
            return 0;
        }

        /// <summary>
        /// Instruction: Increment Value at Memory Location
        /// Function:    M = M + 1
        /// Flags Out:   N, Z
        /// </summary>
        /// <returns></returns>
        public byte INC()
        {
            Fetch();
            temp = (ushort)(fetched + 1);
            Write(addr_abs, (byte)temp);
            SetFlag(FLAGS6502.Z, (temp & 0x00FF) == 0);
            SetFlag(FLAGS6502.N, (temp & 0x0080) != 0);
            return 0;
        }

        /// <summary>
        /// Instruction: Increment X Register
        /// Function:    X = X + 1
        /// Flags Out:   N, Z
        /// </summary>
        /// <returns></returns>
        public byte INX()
        {
            X++;
            SetFlag(FLAGS6502.Z, (X & 0x00FF) == 0);
            SetFlag(FLAGS6502.N, (X & 0x0080) != 0);
            return 0;
        }

        /// <summary>
        /// Instruction: Increment Y Register
        /// Function:    Y = Y + 1
        /// Flags Out:   N, Z
        /// </summary>
        /// <returns></returns>
        public byte INY()
        {
            Y++;
            SetFlag(FLAGS6502.Z, (Y & 0x00FF) == 0);
            SetFlag(FLAGS6502.N, (Y & 0x0080) != 0);
            return 0;
        }

        /// <summary>
        /// Instruction: Jump To Location
        /// Function:    pc = address
        /// </summary>
        /// <returns></returns>
        public byte JMP()
        {
            Pc = addr_abs;
            return 0;
        }

        /// <summary>
        /// Instruction: Jump To Sub-Routine
        /// Function:    Push current pc to stack, pc = address
        /// </summary>
        /// <returns></returns>
        public byte JSR()
        {
            Pc--;
            PushPcOnStack();

            Pc = addr_abs;
            return 0;
        }

        /// <summary>
        /// Instruction: Load The Accumulator
        /// Function:    A = M
        /// Flags Out:   N, Z
        /// </summary>
        /// <returns></returns>
        public byte LDA()
        {
            Fetch();
            A = fetched;
            SetFlag(FLAGS6502.Z, (A & 0x00FF) == 0);
            SetFlag(FLAGS6502.N, (A & 0x0080) != 0);
            return 1;
        }

        /// <summary>
        /// Instruction: Load The X Register
        /// Function:    X = M
        /// Flags Out:   N, Z
        /// </summary>
        /// <returns></returns>
        public byte LDX()
        {
            Fetch();
            X = fetched;
            SetFlag(FLAGS6502.Z, (X & 0x00FF) == 0);
            SetFlag(FLAGS6502.N, (X & 0x0080) != 0);
            return 1;
        }

        /// <summary>
        /// Instruction: Load The Y Register
        /// Function:    Y = M
        /// Flags Out:   N, Z
        /// </summary>
        /// <returns></returns>
        public byte LDY()
        {
            Fetch();
            Y = fetched;
            SetFlag(FLAGS6502.Z, (Y & 0x00FF) == 0);
            SetFlag(FLAGS6502.N, (Y & 0x0080) != 0);
            return 1;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public byte LSR()
        {
            Fetch();
            SetFlag(FLAGS6502.C, (fetched & 0x0001) != 0);
            temp = (byte)(fetched >> 1);
            SetFlag(FLAGS6502.Z, (temp & 0x00FF) == 0x0000);
            SetFlag(FLAGS6502.N, (temp & 0x0080) != 0);
            if (Lookup[opcode].AddressModeFunc == IMP)
                A = (byte)temp;
            else
                Write(addr_abs, (byte)temp);
            return 0;
        }

        /// <summary>
        /// Sadly not all NOPs are equal, Ive added a few here
        /// based on https://wiki.nesdev.com/w/index.php/CPU_unofficial_opcodes
        /// and will add more based on game compatibility, and ultimately
        /// I'd like to cover all illegal opcodes too
        /// </summary>
        /// <returns></returns>
        private byte NOP()
        {
            return opcode switch
            {
                0x1C => 1,
                0x3C => 1,
                0x5C => 1,
                0x7C => 1,
                0xDC => 1,
                0xFC => 1,
                _ => 0
            };
        }

        /// <summary>
        /// Instruction: Bitwise Logic OR
        /// Function:    A = A | M
        /// Flags Out:   N, Z
        /// </summary>
        /// <returns></returns>
        private byte ORA()
        {
            Fetch();
            A = (byte)(A | fetched);
            SetFlag(FLAGS6502.Z, (A & 0x00FF) == 0);
            SetFlag(FLAGS6502.N, (A & 0x0080) != 0);
            return 1;
        }

        /// <summary>
        /// Instruction: Push Accumulator to Stack
        /// Function:    A -> stack
        /// </summary>
        /// <returns></returns>
        private byte PHA()
        {
            PushStack(A);
            return 0;
        }

        /// <summary>
        /// Instruction: Push Status Register to Stack
        /// Function:    status -> stack
        /// Note:        Break flag is set to 1 before push
        /// </summary>
        /// <returns></returns>
        private byte PHP()
        {
            PushStack((byte)(Status | FLAGS6502.B | FLAGS6502.U));
            SetFlag(FLAGS6502.B, false);
            SetFlag(FLAGS6502.U, false);
            return 0;
        }

        /// <summary>
        /// Instruction: Pop Accumulator off Stack
        /// Function:    A <- stack
        /// Flags Out:   N, Z
        /// </summary>
        /// <returns></returns>
        private byte PLA()
        {
            A = PopStack();
            SetFlag(FLAGS6502.Z, (A & 0x00FF) == 0);
            SetFlag(FLAGS6502.N, (A & 0x0080) != 0);
            return 0;
        }

        /// <summary>
        /// Instruction: Pop Status Register off Stack
        /// Function:    Status <- stack
        /// </summary>
        /// <returns></returns>
        private byte PLP()
        {
            Status = (FLAGS6502)PopStack();
            SetFlag(FLAGS6502.U, true);
            return 0;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private byte ROL()
        {
            Fetch();
            temp = (ushort)((fetched << 1) | (Status.HasFlag(FLAGS6502.C) ? 1 : 0));
            SetFlag(FLAGS6502.C, (temp & 0xFF00) != 0);
            SetFlag(FLAGS6502.Z, (temp & 0x00FF) == 0);
            SetFlag(FLAGS6502.N, (temp & 0x0080) != 0);
            if (Lookup[opcode].AddressModeFunc == IMP)
            {
                A = (byte)temp;
            }
            else
            {
                Write(addr_abs, (byte)temp);
            }
            return 0;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private byte ROR()
        {
            Fetch();
            temp = (ushort)(((Status.HasFlag(FLAGS6502.C) ? 1 : 0) << 7) | (fetched >> 1));
            SetFlag(FLAGS6502.C, (fetched & 0x01) != 0);
            SetFlag(FLAGS6502.Z, (temp & 0x00FF) == 0);
            SetFlag(FLAGS6502.N, (temp & 0x0080) != 0);
            if (Lookup[opcode].AddressModeFunc == IMP)
            {
                A = (byte)temp;
            }
            else
            {
                Write(addr_abs, (byte)temp);
            }
            return 0;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private byte RTI()
        {
            Status = (FLAGS6502)PopStack();
            Status &= ~FLAGS6502.B;
            Status &= ~FLAGS6502.U;

            PopStackToPc();
            return 0;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private byte RTS()
        {
            PopStackToPc();
            Pc++;
            return 0;
        }

        /// <summary>
        /// Instruction: Set Carry Flag
        /// Function:    C = 1
        /// </summary>
        /// <returns></returns>
        private byte SEC()
        {
            SetFlag(FLAGS6502.C, true);
            return 0;
        }

        /// <summary>
        /// Instruction: Set Decimal Flag
        /// Function:    D = 1
        /// </summary>
        /// <returns></returns>
        private byte SED()
        {
            SetFlag(FLAGS6502.D, true);
            return 0;
        }

        /// <summary>
        /// Instruction: Set Interrupt Flag / Enable Interrupts
        /// Function:    I = 1
        /// </summary>
        /// <returns></returns>
        private byte SEI()
        {
            SetFlag(FLAGS6502.I, true);
            return 0;
        }

        /// <summary>
        /// Instruction: Store Accumulator at Address
        /// Function:    M = A
        /// </summary>
        /// <returns></returns>
        private byte STA()
        {
            Write(addr_abs, A);
            return 0;
        }

        /// <summary>
        /// Instruction: Store X Register at Address
        /// Function:    M = X
        /// </summary>
        /// <returns></returns>
        private byte STX()
        {
            Write(addr_abs, X);
            return 0;
        }

        /// <summary>
        /// Instruction: Store Y Register at Address
        /// Function:    M = Y
        /// </summary>
        /// <returns></returns>
        private byte STY()
        {
            Write(addr_abs, Y);
            return 0;
        }

        /// <summary>
        /// Instruction: Transfer Accumulator to X Register
        /// Function:    X = A
        /// Flags Out:   N, Z
        /// </summary>
        /// <returns></returns>
        private byte TAX()
        {
            X = A;
            SetFlag(FLAGS6502.Z, (X & 0x00FF) == 0);
            SetFlag(FLAGS6502.N, (X & 0x0080) != 0);
            return 0;
        }

        /// <summary>
        /// Instruction: Transfer Accumulator to Y Register
        /// Function:    Y = A
        /// Flags Out:   N, Z
        /// </summary>
        /// <returns></returns>
        private byte TAY()
        {
            Y = A;
            SetFlag(FLAGS6502.Z, (Y & 0x00FF) == 0);
            SetFlag(FLAGS6502.N, (Y & 0x0080) != 0);
            return 0;
        }

        /// <summary>
        /// Instruction: Transfer Stack Pointer to X Register
        /// Function:    X = stack pointer
        /// Flags Out:   N, Z
        /// </summary>
        /// <returns></returns>
        private byte TSX()
        {
            X = StkPtr;
            SetFlag(FLAGS6502.Z, (X & 0x00FF) == 0);
            SetFlag(FLAGS6502.N, (X & 0x0080) != 0);
            return 0;
        }

        /// <summary>
        /// Instruction: Transfer Stack Pointer to Y Register
        /// Function:    Y = stack pointer
        /// Flags Out:   N, Z
        /// </summary>
        /// <returns></returns>
        private byte TSY()
        {
            Y = StkPtr;
            SetFlag(FLAGS6502.Z, (Y & 0x00FF) == 0);
            SetFlag(FLAGS6502.N, (Y & 0x0080) != 0);
            return 0;
        }

        /// <summary>
        /// Instruction: Transfer X Register to Accumulator
        /// Function:    A = X
        /// Flags Out:   N, Z
        /// </summary>
        /// <returns></returns>
        private byte TXA()
        {
            A = X;
            SetFlag(FLAGS6502.Z, (A & 0x00FF) == 0);
            SetFlag(FLAGS6502.N, (A & 0x0080) != 0);
            return 0;
        }

        /// <summary>
        /// Instruction: Transfer X Register to Stack Pointer
        /// Function:    stack pointer = X
        /// </summary>
        /// <returns></returns>
        private byte TXS()
        {
            StkPtr = X;
            return 0;
        }

        /// <summary>
        /// Instruction: Transfer Y Register to Accumulator
        /// Function:    A = Y
        /// Flags Out:   N, Z
        /// </summary>
        /// <returns></returns>
        private byte TYA()
        {
            A = Y;
            SetFlag(FLAGS6502.Z, (A & 0x00FF) == 0);
            SetFlag(FLAGS6502.N, (A & 0x0080) != 0);
            return 0;
        }

        /// <summary>
        /// This function captures illegal opcodes
        /// </summary>
        /// <returns></returns>
        private byte XXX()
        {
            return 0;
        }

        #endregion

        #region IODevice impl

        /// <summary>
        /// Write to bus
        /// </summary>
        /// <param name="address"></param>
        /// <param name="data"></param>
        public void Write(ushort address, byte data)
        {
            Bus.Write(address, data);
        }


        /// <summary>
        /// Read from bus
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        public byte Read(ushort address)
        {
            return Bus.Read(address);
        }

        /// <summary>
        /// Link to the bus we are attached to.
        /// Invoked by the bus 
        /// </summary>
        /// <param name="bus"></param>
        public void ConnectBus(IIODevice bus)
        {
            Bus = bus;
        }
        #endregion
    }
}
