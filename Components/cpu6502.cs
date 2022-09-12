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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using NESharp.Components.Interfaces;

namespace NESharp.Components
{
    public sealed partial class Cpu6502 : IChip
    {
        #region Constants

        public const ushort PC_START_ADDRESS = 0xFFFC;
        public const ushort IRQ_PC_START_ADDRESS = 0xFFFE;
        public const ushort NMI_PC_START_ADDRESS = 0xFFFA;

        public const byte INITIAL_STACK_POINTER = 0xFD;
        public const ushort STACK_ADDRESS_HIGH_BYTE_MASK = 0x0100;

        public const byte RESET_CYCLE_COUNT = 8;
        public const byte IRQ_CYCLE_COUNT = 7;
        public const byte NMI_CYCLE_COUNT = 8;

        #endregion


        /// <summary>
        /// The status register stores 8 flags. Ive enumerated these here for ease
        /// of access. You can access the status register directly since its public.
        /// The bits have different interpretations depending upon the context and 
        /// instruction being executed.
        /// </summary>
        [Flags]
        public enum FLAGS6502 : byte
        {
            None = 0,
            C = (1 << 0),   // Carry Bit
            Z = (1 << 1),   // Zero
            I = (1 << 2),   // Disable Interrupts
            D = (1 << 3),   // Decimal Mode (unused in this implementation)
            B = (1 << 4),   // Break
            U = (1 << 5),   // Unused
            V = (1 << 6),   // Overflow
            N = (1 << 7),   // Negative
        };


        // This structure and the following vector are used to compile and store
        // the opcode translation table. The 6502 can effectively have 256
        // different instructions. Each of these are stored in a table in numerical
        // order so they can be looked up easily, with no decoding required.
        // Each table entry holds:
        //	Pneumonic : A textual representation of the instruction (used for disassembly)
        //	Opcode Function: A function pointer to the implementation of the opcode
        //	Opcode Address Mode : A function pointer to the implementation of the 
        //						  addressing mechanism used by the instruction
        //	Cycle Count : An integer that represents the base number of clock cycles the
        //				  CPU requires to perform the instruction

        public struct Instruction
        {
            public Instruction(string name, Func<byte> opcodeFunc, Func<byte> addressModeFunc, byte cycles)
            {
                Name = name;
                OpcodeFunc = opcodeFunc;
                AddressModeFunc = addressModeFunc;
                Cycles = cycles;
            }

            public string Name { get; private set; }
            public Func<byte> OpcodeFunc { get; private set; }
            public Func<byte> AddressModeFunc { get; private set; }
            public byte Cycles { get; private set; }
        };

        #region Registers

        public byte A = 0x00;               // Accumulator
        public byte X = 0x00;               // X registerr
        public byte Y = 0x00;               // Y registerr
        public byte StkPtr = 0x00;          // Stack Pointer
        public ushort Pc = 0x0000;          // Program Counter
        public FLAGS6502 Status = 0x00;     // Status register

        #endregion

        #region Private vars

        // Assisstive variables to facilitate emulation
        private byte fetched = 0x00;            // Represents the working input value to the ALU
        private ushort temp = 0x0000;           // A convenience variable used everywhere
        private ushort addr_abs = 0x0000;       // All used memory addresses end up in here
        private ushort addr_rel = 0x00;         // Represents absolute address following a branch
        private byte opcode = 0x00;             // Is the instruction byte
        private byte cycles = 0;                // Counts how many cycles the instruction has remaining
        private uint clock_count = 0;           // A global accumulation of the number of clocks

        #endregion

        public bool DebugEnabled { get; set; }
        public ushort DebugBreakPc { get; set; }
        public Action DebugPcHitCallback { get; set; }
        private StreamWriter logger;

        /// <summary>
        /// Ctor
        /// </summary>
        public Cpu6502()
        {
            // Define all opcodes here. Here we go...
            // index is the opcode value from 0 to 255
            Lookup = new List<Instruction>()
            {
                new("BRK", BRK, IMM, 7 ), new("ORA", ORA, IZX, 6 ), new("???", XXX, IMP, 2 ), new("???", XXX, IMP, 8 ), new("ign", NOP, IMM, 3 ), new("ORA", ORA, ZP0, 3 ), new("ASL", ASL, ZP0, 5 ), new("???", XXX, IMP, 5 ), new("PHP", PHP, IMP, 3 ), new("ORA", ORA, IMM, 2 ), new("ASL", ASL, IMP, 2 ), new("???", XXX, IMP, 2 ), new("???", NOP, IMP, 4 ), new("ORA", ORA, ABS, 4 ), new("ASL", ASL, ABS, 6 ), new("???", XXX, IMP, 6 ),
                new("BPL", BPL, REL, 2 ), new("ORA", ORA, IZY, 5 ), new("???", XXX, IMP, 2 ), new("???", XXX, IMP, 8 ), new("???", NOP, IMP, 4 ), new("ORA", ORA, ZPX, 4 ), new("ASL", ASL, ZPX, 6 ), new("???", XXX, IMP, 6 ), new("CLC", CLC, IMP, 2 ), new("ORA", ORA, ABY, 4 ), new("???", NOP, IMP, 2 ), new("???", XXX, IMP, 7 ), new("???", NOP, IMP, 4 ), new("ORA", ORA, ABX, 4 ), new("ASL", ASL, ABX, 7 ), new("???", XXX, IMP, 7 ),
                new("JSR", JSR, ABS, 6 ), new("AND", AND, IZX, 6 ), new("???", XXX, IMP, 2 ), new("???", XXX, IMP, 8 ), new("BIT", BIT, ZP0, 3 ), new("AND", AND, ZP0, 3 ), new("ROL", ROL, ZP0, 5 ), new("???", XXX, IMP, 5 ), new("PLP", PLP, IMP, 4 ), new("AND", AND, IMM, 2 ), new("ROL", ROL, IMP, 2 ), new("???", XXX, IMP, 2 ), new("BIT", BIT, ABS, 4 ), new("AND", AND, ABS, 4 ), new("ROL", ROL, ABS, 6 ), new("???", XXX, IMP, 6 ),
                new("BMI", BMI, REL, 2 ), new("AND", AND, IZY, 5 ), new("???", XXX, IMP, 2 ), new("???", XXX, IMP, 8 ), new("???", NOP, IMP, 4 ), new("AND", AND, ZPX, 4 ), new("ROL", ROL, ZPX, 6 ), new("???", XXX, IMP, 6 ), new("SEC", SEC, IMP, 2 ), new("AND", AND, ABY, 4 ), new("???", NOP, IMP, 2 ), new("???", XXX, IMP, 7 ), new("???", NOP, IMP, 4 ), new("AND", AND, ABX, 4 ), new("ROL", ROL, ABX, 7 ), new("???", XXX, IMP, 7 ),
                new("RTI", RTI, IMP, 6 ), new("EOR", EOR, IZX, 6 ), new("???", XXX, IMP, 2 ), new("???", XXX, IMP, 8 ), new("ign", NOP, IMM, 3 ), new("EOR", EOR, ZP0, 3 ), new("LSR", LSR, ZP0, 5 ), new("???", XXX, IMP, 5 ), new("PHA", PHA, IMP, 3 ), new("EOR", EOR, IMM, 2 ), new("LSR", LSR, IMP, 2 ), new("???", XXX, IMP, 2 ), new("JMP", JMP, ABS, 3 ), new("EOR", EOR, ABS, 4 ), new("LSR", LSR, ABS, 6 ), new("???", XXX, IMP, 6 ),
                new("BVC", BVC, REL, 2 ), new("EOR", EOR, IZY, 5 ), new("???", XXX, IMP, 2 ), new("???", XXX, IMP, 8 ), new("???", NOP, IMP, 4 ), new("EOR", EOR, ZPX, 4 ), new("LSR", LSR, ZPX, 6 ), new("???", XXX, IMP, 6 ), new("CLI", CLI, IMP, 2 ), new("EOR", EOR, ABY, 4 ), new("???", NOP, IMP, 2 ), new("???", XXX, IMP, 7 ), new("???", NOP, IMP, 4 ), new("EOR", EOR, ABX, 4 ), new("LSR", LSR, ABX, 7 ), new("???", XXX, IMP, 7 ),
                new("RTS", RTS, IMP, 6 ), new("ADC", ADC, IZX, 6 ), new("???", XXX, IMP, 2 ), new("???", XXX, IMP, 8 ), new("ign", NOP, IMM, 3 ), new("ADC", ADC, ZP0, 3 ), new("ROR", ROR, ZP0, 5 ), new("???", XXX, IMP, 5 ), new("PLA", PLA, IMP, 4 ), new("ADC", ADC, IMM, 2 ), new("ROR", ROR, IMP, 2 ), new("???", XXX, IMP, 2 ), new("JMP", JMP, IND, 5 ), new("ADC", ADC, ABS, 4 ), new("ROR", ROR, ABS, 6 ), new("???", XXX, IMP, 6 ),
                new("BVS", BVS, REL, 2 ), new("ADC", ADC, IZY, 5 ), new("???", XXX, IMP, 2 ), new("???", XXX, IMP, 8 ), new("???", NOP, IMP, 4 ), new("ADC", ADC, ZPX, 4 ), new("ROR", ROR, ZPX, 6 ), new("???", XXX, IMP, 6 ), new("SEI", SEI, IMP, 2 ), new("ADC", ADC, ABY, 4 ), new("???", NOP, IMP, 2 ), new("???", XXX, IMP, 7 ), new("???", NOP, IMP, 4 ), new("ADC", ADC, ABX, 4 ), new("ROR", ROR, ABX, 7 ), new("???", XXX, IMP, 7 ),
                new("???", NOP, IMP, 2 ), new("STA", STA, IZX, 6 ), new("???", NOP, IMP, 2 ), new("???", XXX, IMP, 6 ), new("STY", STY, ZP0, 3 ), new("STA", STA, ZP0, 3 ), new("STX", STX, ZP0, 3 ), new("???", XXX, IMP, 3 ), new("DEY", DEY, IMP, 2 ), new("???", NOP, IMP, 2 ), new("TXA", TXA, IMP, 2 ), new("???", XXX, IMP, 2 ), new("STY", STY, ABS, 4 ), new("STA", STA, ABS, 4 ), new("STX", STX, ABS, 4 ), new("???", XXX, IMP, 4 ),
                new("BCC", BCC, REL, 2 ), new("STA", STA, IZY, 6 ), new("???", XXX, IMP, 2 ), new("???", XXX, IMP, 6 ), new("STY", STY, ZPX, 4 ), new("STA", STA, ZPX, 4 ), new("STX", STX, ZPY, 4 ), new("???", XXX, IMP, 4 ), new("TYA", TYA, IMP, 2 ), new("STA", STA, ABY, 5 ), new("TXS", TXS, IMP, 2 ), new("???", XXX, IMP, 5 ), new("???", NOP, IMP, 5 ), new("STA", STA, ABX, 5 ), new("???", XXX, IMP, 5 ), new("???", XXX, IMP, 5 ),
                new("LDY", LDY, IMM, 2 ), new("LDA", LDA, IZX, 6 ), new("LDX", LDX, IMM, 2 ), new("???", XXX, IMP, 6 ), new("LDY", LDY, ZP0, 3 ), new("LDA", LDA, ZP0, 3 ), new("LDX", LDX, ZP0, 3 ), new("???", XXX, IMP, 3 ), new("TAY", TAY, IMP, 2 ), new("LDA", LDA, IMM, 2 ), new("TAX", TAX, IMP, 2 ), new("???", XXX, IMP, 2 ), new("LDY", LDY, ABS, 4 ), new("LDA", LDA, ABS, 4 ), new("LDX", LDX, ABS, 4 ), new("???", XXX, IMP, 4 ),
                new("BCS", BCS, REL, 2 ), new("LDA", LDA, IZY, 5 ), new("???", XXX, IMP, 2 ), new("???", XXX, IMP, 5 ), new("LDY", LDY, ZPX, 4 ), new("LDA", LDA, ZPX, 4 ), new("LDX", LDX, ZPY, 4 ), new("???", XXX, IMP, 4 ), new("CLV", CLV, IMP, 2 ), new("LDA", LDA, ABY, 4 ), new("TSX", TSX, IMP, 2 ), new("???", XXX, IMP, 4 ), new("LDY", LDY, ABX, 4 ), new("LDA", LDA, ABX, 4 ), new("LDX", LDX, ABY, 4 ), new("???", XXX, IMP, 4 ),
                new("CPY", CPY, IMM, 2 ), new("CMP", CMP, IZX, 6 ), new("???", NOP, IMP, 2 ), new("???", XXX, IMP, 8 ), new("CPY", CPY, ZP0, 3 ), new("CMP", CMP, ZP0, 3 ), new("DEC", DEC, ZP0, 5 ), new("???", XXX, IMP, 5 ), new("INY", INY, IMP, 2 ), new("CMP", CMP, IMM, 2 ), new("DEX", DEX, IMP, 2 ), new("???", XXX, IMP, 2 ), new("CPY", CPY, ABS, 4 ), new("CMP", CMP, ABS, 4 ), new("DEC", DEC, ABS, 6 ), new("???", XXX, IMP, 6 ),
                new("BNE", BNE, REL, 2 ), new("CMP", CMP, IZY, 5 ), new("???", XXX, IMP, 2 ), new("???", XXX, IMP, 8 ), new("???", NOP, IMP, 4 ), new("CMP", CMP, ZPX, 4 ), new("DEC", DEC, ZPX, 6 ), new("???", XXX, IMP, 6 ), new("CLD", CLD, IMP, 2 ), new("CMP", CMP, ABY, 4 ), new("NOP", NOP, IMP, 2 ), new("???", XXX, IMP, 7 ), new("???", NOP, IMP, 4 ), new("CMP", CMP, ABX, 4 ), new("DEC", DEC, ABX, 7 ), new("???", XXX, IMP, 7 ),
                new("CPX", CPX, IMM, 2 ), new("SBC", SBC, IZX, 6 ), new("???", NOP, IMP, 2 ), new("???", XXX, IMP, 8 ), new("CPX", CPX, ZP0, 3 ), new("SBC", SBC, ZP0, 3 ), new("INC", INC, ZP0, 5 ), new("???", XXX, IMP, 5 ), new("INX", INX, IMP, 2 ), new("SBC", SBC, IMM, 2 ), new("NOP", NOP, IMP, 2 ), new("???", SBC, IMP, 2 ), new("CPX", CPX, ABS, 4 ), new("SBC", SBC, ABS, 4 ), new("INC", INC, ABS, 6 ), new("???", XXX, IMP, 6 ),
                new("BEQ", BEQ, REL, 2 ), new("SBC", SBC, IZY, 5 ), new("???", XXX, IMP, 2 ), new("???", XXX, IMP, 8 ), new("???", NOP, IMP, 4 ), new("SBC", SBC, ZPX, 4 ), new("INC", INC, ZPX, 6 ), new("???", XXX, IMP, 6 ), new("SED", SED, IMP, 2 ), new("SBC", SBC, ABY, 4 ), new("NOP", NOP, IMP, 2 ), new("???", XXX, IMP, 7 ), new("???", NOP, IMP, 4 ), new("SBC", SBC, ABX, 4 ), new("INC", INC, ABX, 7 ), new("???", XXX, IMP, 7 ),
            };
        }

        public IIODevice Bus { get; set; }
        public List<Instruction> Lookup = new();

        // EXTERNAL EVENTS

        /// <summary>
        /// Forces the 6502 into a known state. This is hard-wired inside the CPU. The
        /// registers are set to 0x00, the status register is cleared except for unused
        /// bit which remains at 1. An absolute address is read from location 0xFFFC
        /// which contains a second address that the program counter is set to. This 
        /// allows the programmer to jump to a known and programmable location in the
        /// memory to start executing from. Typically the programmer would set the value
        /// </summary>
        public void Reset(bool hardReset = false)
        {
            // Get address to set program counter to
            // This is stored little indian starting at 0xFFFC 
            Pc = ReadAsAddress(PC_START_ADDRESS);

            // Reset registers
            A = 0;
            X = 0;
            Y = 0;
            StkPtr = INITIAL_STACK_POINTER;
            Status = FLAGS6502.U;

            // Clear internals
            addr_abs = 0x0000;
            addr_rel = 0x0000;
            fetched = 0x00;

            // Reset takes time
            cycles = RESET_CYCLE_COUNT;
            clock_count = 0;
        }

        /// <summary>
        /// Interrupt requests are a complex operation and only happen if the
        /// "disable interrupt" flag is 0. IRQs can happen at any time, but
        /// you dont want them to be destructive to the operation of the running 
        /// program. Therefore the current instruction is allowed to finish
        /// (which I facilitate by doing the whole thing when cycles == 0) and 
        /// then the current program counter is stored on the stack. Then the
        /// current status register is stored on the stack. When the routine
        /// that services the interrupt has finished, the status register
        /// and program counter can be restored to how they where before it 
        /// occurred. This is impemented by the "RTI" instruction. Once the IRQ
        /// has happened, in a similar way to a reset, a programmable address
        /// is read form hard coded location 0xFFFE, which is subsequently
        /// set to the program counter.
        /// </summary>
        public void IRQ()
        {
            // Check if "Disable Interrupts" is set
            if (Status.HasFlag(FLAGS6502.I)) { return; }

            // Push current current PC to the stack (two pushes)
            PushPcOnStack();

            // Then Push the status register to the stack
            Status |= FLAGS6502.B;
            Status |= FLAGS6502.U;
            PushStack((byte)Status);

            // After writing to the stack, set the Interupt flag to 1
            // to prevent other interrupts
            Status |= FLAGS6502.I;

            // Read new program counter location from fixed address
            Pc = ReadAsAddress(IRQ_PC_START_ADDRESS);

            // IRQs take time
            cycles = IRQ_CYCLE_COUNT;
        }

        /// <summary>
        /// A Non-Maskable Interrupt cannot be ignored. It behaves in exactly the
        /// same way as a regular IRQ, but reads the new program counter address
        /// form location 0xFFFA.
        /// </summary>
        public void NMI()
        {
            // Push current current PC to the stack (two pushes)
            PushStack((byte)((Pc >> 8) & 0x00FF)); // Store the high byte (& 0x00FF clears the high portion)
            PushStack((byte)(Pc & 0x00FF)); // Store the low byte

            // Then Push the status register to the stack
            Status |= FLAGS6502.B;
            Status |= FLAGS6502.U;
            PushStack((byte)Status);

            // After writing to the stack, set the Interupt flag to 1
            // to prevent other interrupts
            Status |= FLAGS6502.I;

            // Read new program counter location from fixed address
            Pc = ReadAsAddress(NMI_PC_START_ADDRESS);

            // NMIs take time
            cycles = NMI_CYCLE_COUNT;
        }

        /// <summary>
        /// Perform one clock cycles worth of emulation
        /// </summary>
        public void Clock()
        {
            // Non clock cycle accurate emulation
            // The result is calculated immediatly based on an opperation cycle value
            if (cycles == 0)
            {
                if (DebugEnabled)
                {
                    // Init logger
                    logger = logger ??= File.CreateText("cpu.txt");
                    // Log Pc before incrementing
                    logger.WriteLine($"{Pc:X4}  A:{A:X2} X:{X:X2} Y:{Y:X2} P:{(byte)Status:X2} SP:{StkPtr:X2}  CYC:{clock_count}");

                    // Usefull to break at a specific prg location
                    if (DebugBreakPc != 0x0000 && Pc == DebugBreakPc)
                    {
                        Debugger.Break();
                        // Disable further breaks
                        DebugBreakPc = 0;
                        // force push logging buffer to file
                        logger.Flush();
                        // Notify if defined
                        DebugPcHitCallback?.Invoke();
                    }
                }

                // Read next instruction byte. This 8-bit value is used to index
                // the translation table to get the relevant information about
                // how to implement the instruction
                opcode = CpuRead(Pc, false);

                // Always set the unused status flag bit to 1
                Status |= FLAGS6502.U;

                // Increment program counter, we read the opcode byte
                Pc++;

                // Get Starting number of cycles
                cycles = Lookup[opcode].Cycles;

                // Perform fetch of intermmediate data using the
                // required addressing mode
                byte extra_cycles1 = Lookup[opcode].AddressModeFunc();
                // Perform the opperation
                byte extra_cycles2 = Lookup[opcode].OpcodeFunc();

                // The addressmode and opcode may have altered the number
                // of cycles this instruction requires before its completed
                cycles += (byte)(extra_cycles1 & extra_cycles2);

                // Always set the unused status flag bit to 1
                Status |= FLAGS6502.U;

            }

            // Increment global clock count - This is actually unused unless logging is enabled
            // but I've kept it in because its a handy watch variable for debugging
            clock_count++;

            // Decrement the number of cycles remaining for this instruction
            cycles--;
        }

        #region Helpers

        /// <summary>
        /// Indicates the current instruction has completed by returning true. This is
        /// a utility function to enable "step-by-step" execution, without manually 
        /// clocking every cycle
        /// </summary>
        public bool HasCompletedCycle
        {
            get { return cycles == 0; }
        }

        private void PushStack(byte value)
        {
            CpuWrite((ushort)(STACK_ADDRESS_HIGH_BYTE_MASK + StkPtr), value);
            StkPtr--;
        }

        private void PushPcOnStack()
        {
            PushStack((byte)((Pc >> 8) & 0x00FF)); // Store the high byte (& 0x00FF clears the high portion)
            PushStack((byte)(Pc & 0x00FF)); // Store the low byte
        }

        private byte PopStack()
        {
            StkPtr++;
            return CpuRead((ushort)(STACK_ADDRESS_HIGH_BYTE_MASK + StkPtr), false);
        }

        private void PopStackToPc()
        {
            Pc = PopStack();
            Pc |= (ushort)(PopStack() << 8);

        }


        private ushort ReadAsAddress(ushort startAddress)
        {
            addr_abs = startAddress;
            ushort low = CpuRead((ushort)(addr_abs + 0), false);
            ushort high = CpuRead((ushort)(addr_abs + 1), false);

            // return the result as an address
            return (ushort)((high << 8) | low);
        }

        /// <summary>
        /// Read the data at current program counter and increments it after
        /// </summary>
        /// <returns></returns>
        private byte ReadPc()
        {
            return CpuRead(Pc++, false);

        }

        /// <summary>
        /// Read the next two bytes at current program counter, increments and return as a ushort address
        /// </summary>
        /// <returns></returns>
        private ushort ReadPcAsAddress()
        {
            ushort low = ReadPc();
            ushort high = ReadPc();

            // return the result as an address
            return (ushort)((high << 8) | low);
        }

        /// <summary>
        /// Set of unset a flag based on a condition
        /// </summary>
        /// <param name="flag"></param>
        /// <param name="isSet"></param>
        private void SetFlag(FLAGS6502 flag, bool isSet)
        {
            Status = isSet ? Status | flag : Status & ~flag;
        }

        #endregion

        #region IODevice impl

        /// <summary>
        /// Write to bus
        /// </summary>
        /// <param name="address"></param>
        /// <param name="data"></param>
        public void CpuWrite(ushort address, byte data)
        {
            Bus.CpuWrite(address, data);
        }


        /// <summary>
        /// Read from bus
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        public byte CpuRead(ushort address, bool asReadOnly)
        {
            return Bus.CpuRead(address, asReadOnly);
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
