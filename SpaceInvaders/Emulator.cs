using System;
using System.IO;
using System.Reflection;
using Z80;

// http://www.computerarcheology.com/Arcade/SpaceInvaders/
// http://brentradio.com/images/SpaceInvaders/Midway8080SystemBoards.html

// https://code.google.com/archive/p/cogwheel/

/*
 * http://www.emutalk.net/threads/38177-Space-Invaders
 * 
Space Invaders, (C) Taito 1978, Midway 1979

CPU: Intel 8080 @ 2MHz (CPU similar to the (newer) Zilog Z80)

Interrupts: $cf (RST 8) at the start of vblank, $d7 (RST $10) at the end of vblank.

Video: 256(x)*224(y) @ 60Hz, vertical monitor. Colours are simulated with a
plastic transparent overlay and a background picture.
Video hardware is very simple: 7168 bytes 1bpp bitmap (32 bytes per scanline).

Sound: SN76477 and samples.

Memory map:
	ROM
	$0000-$07ff:	invaders.h
	$0800-$0fff:	invaders.g
	$1000-$17ff:	invaders.f
	$1800-$1fff:	invaders.e
	
	RAM
	$2000-$23ff:	work RAM
	$2400-$3fff:	video RAM
	
	$4000-:		RAM mirror

Ports:
	Read 1
	BIT	0	coin (0 when active)
		1	P2 start button
		2	P1 start button
		3	?
		4	P1 shoot button
		5	P1 joystick left
		6	P1 joystick right
		7	?
	
	Read 2
	BIT	0,1	dipswitch number of lives (0:3,1:4,2:5,3:6)
		2	tilt 'button'
		3	dipswitch bonus life at 1:1000,0:1500
		4	P2 shoot button
		5	P2 joystick left
		6	P2 joystick right
		7	dipswitch coin info 1:off,0:on
	
	Read 3		shift register result
	
	Write 2		shift register result offset (bits 0,1,2)
	Write 3		sound related
	Write 4		fill shift register
	Write 5		sound related
	Write 6		strange 'debug' port? eg. it writes to this port when
			it writes text to the screen (0=a,1=b,2=c, etc)
	
	(write ports 3,5,6 can be left unemulated, read port 1=$01 and 2=$00
	will make the game run, but but only in attract mode)

I haven't looked into sound details.

16 bit shift register:
	f              0	bit
	xxxxxxxxyyyyyyyy
	
	Writing to port 4 shifts x into y, and the new value into x, eg.
	$0000,
	write $aa -> $aa00,
	write $ff -> $ffaa,
	write $12 -> $12ff, ..
	
	Writing to port 2 (bits 0,1,2) sets the offset for the 8 bit result, eg.
	offset 0:
	rrrrrrrr		result=xxxxxxxx
	xxxxxxxxyyyyyyyy
	
	offset 2:
	  rrrrrrrr	result=xxxxxxyy
	xxxxxxxxyyyyyyyy
	
	offset 7:
	       rrrrrrrr	result=xyyyyyyy
	xxxxxxxxyyyyyyyy
	
	Reading from port 3 returns said result.

Overlay dimensions (screen rotated 90 degrees anti-clockwise):
	,_______________________________.
	|WHITE            ^             |
	|                32             |
	|                 v             |
	|-------------------------------|
	|RED              ^             |
	|                32             |
	|                 v             |
	|-------------------------------|
	|WHITE                          |
	|         < 224 >               |
	|                               |
	|                 ^             |
	|                120            |
	|                 v             |
	|                               |
	|                               |
	|                               |
	|-------------------------------|
	|GREEN                          |
	| ^                  ^          |
	|56        ^        56          |
	| v       72         v          |
	|____      v      ______________|
	|  ^  |          | ^            |
	|<16> |  < 118 > |16   < 122 >  |
	|  v  |          | v            |
	|WHITE|          |         WHITE|
	`-------------------------------'
	
	Way of out of proportion :P 


Sound bits:
Port 3
- bit 0: Saucer sound via sn76477
- bit 1: shot
- bit 2: Base hit
- bit 3: invader hit
- bit 4: bonus base

Port 5:
- bits 0 to 3: walking sounds
- bit 4: saucer hit


 */

namespace SpaceInvaders
{

	public delegate void DoneScreen ();

	public class Emulator
	{
		public event DoneScreen OneScreen;

		private MemoryCallbackSystem memoryCallbacks;
		private Z80.Z80 cpu;
		private const int MEMORY_SIZE = 0x4000;
		private byte [] memory;
		private int videoCount = 224 * 256;
		//private int interruptType = 1;

		public BmpMaker bmp = new BmpMaker (224, 256); // (224, 256);

		public Emulator ()
		{
			memoryCallbacks = new MemoryCallbackSystem ();
			cpu = new Z80.Z80 ();
			cpu.RegisterSP = 0x00;
			cpu.ReadHardware = ReadPort;
			cpu.WriteHardware = WritePort;
			cpu.MemoryCallbacks = memoryCallbacks;
			cpu.ReadMemory = ReadMemory;
			cpu.WriteMemory = WriteMemory;
			cpu.IRQCallback = IRQCallback;
			cpu.NMICallback = NMICallback;
			cpu.InterruptMode = 2;

			memory = new byte [MEMORY_SIZE];
			// load rom
			var assembly = typeof (App).GetTypeInfo ().Assembly;
			Stream stream = assembly.GetManifestResourceStream ("SpaceInvaders.invaders.h");
			stream.Read (memory, 0x0000, 0x0800);
			stream = assembly.GetManifestResourceStream ("SpaceInvaders.invaders.g");
			stream.Read (memory, 0x0800, 0x0800);
			stream = assembly.GetManifestResourceStream ("SpaceInvaders.invaders.f");
			stream.Read (memory, 0x1000, 0x0800);
			stream = assembly.GetManifestResourceStream ("SpaceInvaders.invaders.e");
			stream.Read (memory, 0x1800, 0x0800);
		}

		public void Execute (int cycles)
		{
			cpu.ExecuteCycles (cycles);
			cpu.Interrupt = true;
		}
		// Graphics

		int minX = 1; int maxX = 222;
		int minY = 1; int maxY = 254;
		private void PlotPixel (int x, int y, int value, int bit)
		{
			var bt = (value >> bit) & 1;
			y = y - bit;
			var r = 0;
			var g = 0;
			var b = 0;
			if (bt > 0) {
				if (y >= 184 && y <= 238 && x >= 0 && x <= 223)
					g = 255;
				else if (y >= 240 && y <= 247 && x >= 16 && x <= 133)
					g = 255;
				else if (y >= (247 - 215) && y >= (247 - 184) && x >= 0 && x <= 233) {
					g = 255;
					b = 255;
					r = 255;
				} else {
					r = 255;
				}
			}
			/*
			var index = y * (4 * 224) + x * 4;
			video [index] = r;
			video [index + 1] = g;
			video [index + 2] = b;
			video [index + 3] = 255;
			*/
			try {
				bmp.SetPixel (255 - y, x, r, g, b);
			} catch (Exception err) {
				System.Diagnostics.Debug.WriteLine ("bug video");
			}
			if (x > maxX) {
				maxX = x;
				System.Diagnostics.Debug.WriteLine ("MaxX: " + maxX);
			} else if (x < minX) {
				minX = x;
				System.Diagnostics.Debug.WriteLine ("MinX: " + minX);
			}
			if (y > maxY) {
				maxY = y;
				System.Diagnostics.Debug.WriteLine ("MaxY: " + maxY);
			} else if (y < minY) {
				minY = y;
				System.Diagnostics.Debug.WriteLine ("MinY: " + minY);
			}
		}

		void NMICallback ()
		{
			cpu.NonMaskableInterrupt = false;
		}

		void IRQCallback ()
		{
			cpu.Interrupt = false;
		}

		// Memory

		private byte ReadMemory (ushort address)
		{
			if (address < MEMORY_SIZE)
				return memory [address];
			else
				return 0;
		}

		public void WriteMemory (ushort address, byte value)
		{
			if (address >= 0x2000) {
				if (address < 0x4000) {
					memory [address] = value;
					if (address >= 0x2400) {
						// Update video memory
						int b = address - 0x2400;
						var y = ~(((b & 0x1f) * 8) & 0xFF) & 0xFF;
						var x = b >> 5;
						for (var i = 0; i < 8; ++i) {
							PlotPixel (x, y, value, i);
						}
					}
					//} else {
					//	System.Diagnostics.Debug.WriteLine ("Writing out of RAM :(");
				}
				//} else {
				//	System.Diagnostics.Debug.WriteLine ("Writing to ROM :(");
			}
		}

		// Hardware

		private byte _p1 = 0;
		private byte _p2 = 0;
		private byte _p3 = 0;
		private byte _p5 = 0;
		private byte _s0 = 0;
		private byte _s1 = 0;
		private byte _soff = 0;


		private byte ReadPort (ushort port)
		{
			switch (port) {
			case 0:
				return 0x0f;
			case 1:
				return _p1;
			case 2:
				return _p2;
			case 3:
				ushort w = (ushort)((_s1 << 8) + _s0);
				return (byte)((w >> (8 - _soff)) & 0xff);
			}
			return 0x00;
		}

		private void WritePort (ushort port, byte value)
		{
			switch (port) {
			case 1:
				_p1 = value;
				break;
			case 2:
				_soff = (byte)(value & 0x07);
				break;
			case 3:
				_p3 = value;
				// sound
				break;
			case 4:
				_s0 = _s1;
				_s1 = value;
				break;
			case 5:
				_p5 = value;
				// sound
				break;
			case 6:
				//System.Diagnostics.Debug.WriteLine ("{0}", Convert.ToChar (value + 65));
				break;
			}
		}

	}
}