using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NESharp.Components.Interfaces;

namespace NESharp.Components
{
    public class Apu2A03 : IChip
    {
        #region Structs

        public class Sequencer
        {
            public uint sequence = 0x00000000;
            public uint new_sequence = 0x00000000;
            public ushort timer = 0x0000;
            public ushort reload = 0x0000;
            public byte output = 0x00;

            // Pass in a lambda function to manipulate the sequence as required
            // by the owner of this sequencer module
            public byte Clock(bool bEnable, Action<uint> funcManip)
            {
                if (bEnable)
                {
                    timer--;
                    if (timer == 0xFFFF)
                    {
                        timer = reload;
                        funcManip(sequence);
                        output = (byte)(sequence & 0x00000001);
                    }
                }
                return output;
            }
        }

        public class LengthCounter
        {
            public byte counter = 0x00;
            public byte Clock(bool bEnable, bool bHalt)
            {
                if (!bEnable)
                    counter = 0;
                else
                if (counter > 0 && !bHalt)
                    counter--;
                return counter;
            }
        }

        public class Envelope
        {
            public void Clock(bool bLoop)
            {
                if (!start)
                {
                    if (divider_count == 0)
                    {
                        divider_count = volume;

                        if (decay_count == 0)
                        {
                            if (bLoop)
                            {
                                decay_count = 15;
                            }

                        }
                        else
                            decay_count--;
                    }
                    else
                        divider_count--;
                }
                else
                {
                    start = false;
                    decay_count = 15;
                    divider_count = volume;
                }

                if (disable)
                {
                    output = volume;
                }
                else
                {
                    output = decay_count;
                }
            }

            public bool start = false;
            public bool disable = false;
            public ushort divider_count = 0;
            public ushort volume = 0;
            public ushort output = 0;
            public ushort decay_count = 0;
        }

        public class Oscpulse
        {
            public double frequency = 0;
            public double dutycycle = 0;
            public double amplitude = 1;
            public double pi = 3.14159;
            public double harmonics = 20;

            public double Sample(double t)
            {
                double a = 0;
                double b = 0;
                double p = dutycycle * 2.0 * pi;

                double approxsin(float t)

                {
                    float j = (float)(t * 0.15915);
                    j = j - (int)j;
                    return 20.785 * j * (j - 0.5) * (j - 1.0f);
                };

                for (double n = 1; n < harmonics; n++)
                {
                    double c = n * frequency * 2.0 * pi * t;
                    a += -approxsin((float)c) / n;
                    b += -approxsin((float)(c - p * n)) / n;

                    //a += -sin(c) / n;
                    //b += -sin(c - p * n) / n;
                }

                return (2.0 * amplitude / pi) * (a - b);
            }
        }

        public class Sweeper
        {
            public bool enabled = false;
            public bool down = false;
            public bool reload = false;
            public byte shift = 0x00;
            public byte timer = 0x00;
            public byte period = 0x00;
            public ushort change = 0;
            public bool mute = false;

            public void Track(ushort target)
            {
                if (enabled)
                {
                    change = (ushort)(target >> shift);
                    mute = (target < 8) || (target > 0x7FF);
                }
            }

            public bool Clock(ushort target, bool channel)
            {
                bool changed = false;
                if (timer == 0 && enabled && shift > 0 && !mute)
                {
                    if (target >= 8 && change < 0x07FF)
                    {
                        if (down)
                        {
                            target -= (ushort)(change - (channel ? 1 : 0));
                        }
                        else
                        {
                            target += change;
                        }
                        changed = true;
                    }
                }

                //if (enabled)
                {
                    if (timer == 0 || reload)
                    {
                        timer = period;
                        reload = false;
                    }
                    else
                        timer--;

                    mute = (target < 8) || (target > 0x7FF);
                }

                return changed;
            }
        };

        #endregion

        private IIODevice bus { get; set; }
        private uint frame_clock_counter = 0;
        private uint clock_counter = 0;
        private bool bUseRawMode = false;

        private double dGlobalTime = 0.0;

        // Square Wave Pulse Channel 1
        private bool pulse1_enable = false;
        private bool pulse1_halt = false;
        private double pulse1_sample = 0.0;
        private double pulse1_output = 0.0;
        private Sequencer pulse1_seq = new Sequencer();
        private Oscpulse pulse1_osc = new Oscpulse();
        private Envelope pulse1_env= new Envelope();
        private LengthCounter pulse1_lc = new LengthCounter();
        private Sweeper pulse1_sweep = new Sweeper();

        // Square Wave Pulse Channel 2
        private bool pulse2_enable = false;
        private bool pulse2_halt = false;
        private double pulse2_sample = 0.0;
        private double pulse2_output = 0.0;
        private Sequencer pulse2_seq = new Sequencer();
        private Oscpulse pulse2_osc = new Oscpulse();
        private Envelope pulse2_env = new Envelope();
        private LengthCounter pulse2_lc = new LengthCounter();
        private Sweeper pulse2_sweep = new Sweeper();

        // Noise Channel
        private bool noise_enable = false;
        private bool noise_halt = false;
        private Envelope noise_env = new Envelope();
        private LengthCounter noise_lc = new LengthCounter();
        private Sequencer noise_seq = new Sequencer();
        private double noise_sample = 0;
        private double noise_output = 0;

        private static byte[] length_table = new byte[]
        {
            10, 254, 20,  2, 40,  4, 80,  6,
            160,   8, 60, 10, 14, 12, 26, 14,
            12,  16, 24, 18, 48, 20, 96, 22,
            192,  24, 72, 26, 16, 28, 32, 30
        };

        public ushort pulse1_visual = 0;
        public ushort pulse2_visual = 0;
        public ushort noise_visual = 0;
        public ushort triangle_visual = 0;

        public Apu2A03()
        {
        noise_seq.sequence = 0xDBDB;
        }

        public double GetOutputSample()
        {
            if (bUseRawMode)
            {
                return (pulse1_sample - 0.5) * 0.5
                       + (pulse2_sample - 0.5) * 0.5;
            }
            else
            {
                return ((1.0 * pulse1_output) - 0.8) * 0.1 +
                       ((1.0 * pulse2_output) - 0.8) * 0.1 +
                       ((2.0 * (noise_output - 0.5))) * 0.1;
            }
        }

        public void CpuWrite(ushort address, byte data)
        {
            switch (address)
            {
                case 0x4000:
                    switch ((data & 0xC0) >> 6)
                    {
                        case 0x00: pulse1_seq.new_sequence = 0b01000000; pulse1_osc.dutycycle = 0.125; break;
                        case 0x01: pulse1_seq.new_sequence = 0b01100000; pulse1_osc.dutycycle = 0.250; break;
                        case 0x02: pulse1_seq.new_sequence = 0b01111000; pulse1_osc.dutycycle = 0.500; break;
                        case 0x03: pulse1_seq.new_sequence = 0b10011111; pulse1_osc.dutycycle = 0.750; break;
                    }
                    pulse1_seq.sequence = pulse1_seq.new_sequence;
                    pulse1_halt = (data & 0x20) != 0;
                    pulse1_env.volume = (ushort)(data & 0x0F);
                    pulse1_env.disable = (data & 0x10) != 0;
                    break;

                case 0x4001:
                    pulse1_sweep.enabled = (data & 0x80) != 0;
                    pulse1_sweep.period = (byte)((data & 0x70) >> 4);
                    pulse1_sweep.down = (data & 0x08) != 0;
                    pulse1_sweep.shift = (byte)(data & 0x07);
                    pulse1_sweep.reload = true;
                    break;

                case 0x4002:
                    pulse1_seq.reload = (ushort)((pulse1_seq.reload & 0xFF00) | data);
                    break;

                case 0x4003:
                    pulse1_seq.reload = (ushort)((data & 0x07) << 8 | (pulse1_seq.reload & 0x00FF));
                    pulse1_seq.timer = pulse1_seq.reload;
                    pulse1_seq.sequence = pulse1_seq.new_sequence;
                    pulse1_lc.counter = length_table[(data & 0xF8) >> 3];
                    pulse1_env.start = true;
                    break;

                case 0x4004:
                    switch ((data & 0xC0) >> 6)
                    {
                        case 0x00: pulse2_seq.new_sequence = 0b01000000; pulse2_osc.dutycycle = 0.125; break;
                        case 0x01: pulse2_seq.new_sequence = 0b01100000; pulse2_osc.dutycycle = 0.250; break;
                        case 0x02: pulse2_seq.new_sequence = 0b01111000; pulse2_osc.dutycycle = 0.500; break;
                        case 0x03: pulse2_seq.new_sequence = 0b10011111; pulse2_osc.dutycycle = 0.750; break;
                    }
                    pulse2_seq.sequence = pulse2_seq.new_sequence;
                    pulse2_halt = (data & 0x20) != 0;
                    pulse2_env.volume = (ushort)((data & 0x0F));
                    pulse2_env.disable = (data & 0x10) != 0;
                    break;

                case 0x4005:
                    pulse2_sweep.enabled = (data & 0x80) != 0;
                    pulse2_sweep.period = (byte)((data & 0x70) >> 4);
                    pulse2_sweep.down = (data & 0x08) != 0;
                    pulse2_sweep.shift = (byte)(data & 0x07);
                    pulse2_sweep.reload = true;
                    break;

                case 0x4006:
                    pulse2_seq.reload = (ushort)((pulse2_seq.reload & 0xFF00) | data);
                    break;

                case 0x4007:
                    pulse2_seq.reload = (ushort)((data & 0x07) << 8 | (pulse2_seq.reload & 0x00FF));
                    pulse2_seq.timer = pulse2_seq.reload;
                    pulse2_seq.sequence = pulse2_seq.new_sequence;
                    pulse2_lc.counter = length_table[(data & 0xF8) >> 3];
                    pulse2_env.start = true;

                    break;

                case 0x4008:
                    break;

                case 0x400C:
                    noise_env.volume = (ushort)((data & 0x0F));
                    noise_env.disable = (data & 0x10) != 0;
                    noise_halt = (data & 0x20) != 0;
                    break;

                case 0x400E:
                    switch (data & 0x0F)
                    {
                        case 0x00: noise_seq.reload = 0; break;
                        case 0x01: noise_seq.reload = 4; break;
                        case 0x02: noise_seq.reload = 8; break;
                        case 0x03: noise_seq.reload = 16; break;
                        case 0x04: noise_seq.reload = 32; break;
                        case 0x05: noise_seq.reload = 64; break;
                        case 0x06: noise_seq.reload = 96; break;
                        case 0x07: noise_seq.reload = 128; break;
                        case 0x08: noise_seq.reload = 160; break;
                        case 0x09: noise_seq.reload = 202; break;
                        case 0x0A: noise_seq.reload = 254; break;
                        case 0x0B: noise_seq.reload = 380; break;
                        case 0x0C: noise_seq.reload = 508; break;
                        case 0x0D: noise_seq.reload = 1016; break;
                        case 0x0E: noise_seq.reload = 2034; break;
                        case 0x0F: noise_seq.reload = 4068; break;
                    }
                    break;

                case 0x4015: // APU STATUS
                    pulse1_enable = (data & 0x01) != 0;
                    pulse2_enable = (data & 0x02) != 0;
                    noise_enable = (data & 0x04) != 0;
                    break;

                case 0x400F:
                    pulse1_env.start = true;
                    pulse2_env.start = true;
                    noise_env.start = true;
                    noise_lc.counter = length_table[(data & 0xF8) >> 3];
                    break;
            }
        }

        public byte CpuRead(ushort address, bool asReadonly)
        {
            byte data = 0x00;

            if (address == 0x4015)
            {
                //	data |= (pulse1_lc.counter > 0) ? 0x01 : 0x00;
                //	data |= (pulse2_lc.counter > 0) ? 0x02 : 0x00;		
                //	data |= (noise_lc.counter > 0) ? 0x04 : 0x00;
            }

            return data;
        }

        public IIODevice Bus { get; }
        public void ConnectBus(IIODevice bus)
        {
            this.bus = bus;
        }

        public void Reset(bool hardReset = false)
        {
        }

        public void Clock()
        {
            // Depending on the frame count, we set a flag to tell 
            // us where we are in the sequence. Essentially, changes
            // to notes only occur at these intervals, meaning, in a
            // way, this is responsible for ensuring musical time is
            // maintained.
            bool bQuarterFrameClock = false;
            bool bHalfFrameClock = false;

            dGlobalTime += (0.3333333333 / 1789773);


            if (clock_counter % 6 == 0)
            {
                frame_clock_counter++;


                // 4-Step Sequence Mode
                if (frame_clock_counter == 3729)
                {
                    bQuarterFrameClock = true;
                }

                if (frame_clock_counter == 7457)
                {
                    bQuarterFrameClock = true;
                    bHalfFrameClock = true;
                }

                if (frame_clock_counter == 11186)
                {
                    bQuarterFrameClock = true;
                }

                if (frame_clock_counter == 14916)
                {
                    bQuarterFrameClock = true;
                    bHalfFrameClock = true;
                    frame_clock_counter = 0;
                }

                // Update functional units

                // Quater frame "beats" adjust the volume envelope
                if (bQuarterFrameClock)
                {
                    pulse1_env.Clock(pulse1_halt);
                    pulse2_env.Clock(pulse2_halt);
                    noise_env.Clock(noise_halt);
                }


                // Half frame "beats" adjust the note length and
                // frequency sweepers
                if (bHalfFrameClock)
                {
                    pulse1_lc.Clock(pulse1_enable, pulse1_halt);
                    pulse2_lc.Clock(pulse2_enable, pulse2_halt);
                    noise_lc.Clock(noise_enable, noise_halt);
                    pulse1_sweep.Clock(pulse1_seq.reload, false);
                    pulse2_sweep.Clock(pulse2_seq.reload, true);
                }

                //	if (bUseRawMode)
                {
                    // Update Pulse1 Channel ================================
                    pulse1_seq.Clock(pulse1_enable, s =>
                    {
                        // Shift right by 1 bit, wrapping around
                        s = ((s & 0x0001) << 7) | ((s & 0x00FE) >> 1);
                    });

                    //	pulse1_sample = (double)pulse1_seq.output;
                }
                //else
                {
                    pulse1_osc.frequency = 1789773.0 / (16.0 * (double)(pulse1_seq.reload + 1));
                    pulse1_osc.amplitude = (double)(pulse1_env.output - 1) / 16.0;
                    pulse1_sample = pulse1_osc.Sample(dGlobalTime);

                    if (pulse1_lc.counter > 0 && pulse1_seq.timer >= 8 && !pulse1_sweep.mute && pulse1_env.output > 2)
                        pulse1_output += (pulse1_sample - pulse1_output) * 0.5;
                    else
                        pulse1_output = 0;
                }

                //if (bUseRawMode)
                {
                    // Update Pulse1 Channel ================================
                    pulse2_seq.Clock(pulse2_enable, s =>
                    {
                        // Shift right by 1 bit, wrapping around
                        s = ((s & 0x0001) << 7) | ((s & 0x00FE) >> 1);
                    });

                    //	pulse2_sample = (double)pulse2_seq.output;

                }
                //	else
                {
                    pulse2_osc.frequency = 1789773.0 / (16.0 * (double)(pulse2_seq.reload + 1));
                    pulse2_osc.amplitude = (double)(pulse2_env.output - 1) / 16.0;
                    pulse2_sample = pulse2_osc.Sample(dGlobalTime);

                    if (pulse2_lc.counter > 0 && pulse2_seq.timer >= 8 && !pulse2_sweep.mute && pulse2_env.output > 2)
                        pulse2_output += (pulse2_sample - pulse2_output) * 0.5;
                    else
                        pulse2_output = 0;
                }


                noise_seq.Clock(noise_enable, s =>
                {
                    s = (((s & 0x0001) ^ ((s & 0x0002) >> 1)) << 14) | ((s & 0x7FFF) >> 1);
                });

                if (noise_lc.counter > 0 && noise_seq.timer >= 8)
                {
                    noise_output = (double)noise_seq.output * ((double)(noise_env.output - 1) / 16.0);
                }

                if (!pulse1_enable) pulse1_output = 0;
                if (!pulse2_enable) pulse2_output = 0;
                if (!noise_enable) noise_output = 0;

            }

            // Frequency sweepers change at high frequency
            pulse1_sweep.Track(pulse1_seq.reload);
            pulse2_sweep.Track(pulse2_seq.reload);

            pulse1_visual = (pulse1_enable && pulse1_env.output > 1 && !pulse1_sweep.mute) ? pulse1_seq.reload : (ushort)2047;
            pulse2_visual = (pulse2_enable && pulse2_env.output > 1 && !pulse2_sweep.mute) ? pulse2_seq.reload : (ushort)2047;
            noise_visual = (noise_enable && noise_env.output > 1) ? noise_seq.reload : (ushort)2047;

            clock_counter++;
        }
    }
}
