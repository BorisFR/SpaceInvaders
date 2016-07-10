using System;
using System.IO;
using System.Reflection;
using eZet.i8080.Emulator;

namespace SpaceInvaders
{
	public class Emu8080 : IVideoDevice
	{
		private System8080 system;

		public Emu8080 ()
		{
			system = new System8080 ();
			system.loadProgram (loadInvaders (), 0);
			system.AddVideo (this);
		}

		public void Run ()
		{
			system.boot ();
		}

		private MemoryStream loadInvaders ()
		{
			var ms = new MemoryStream ();
			var assembly = typeof (App).GetTypeInfo ().Assembly;
			Stream stream = assembly.GetManifestResourceStream ("SpaceInvaders.invaders.h");
			stream.CopyTo (ms);
			stream = assembly.GetManifestResourceStream ("SpaceInvaders.invaders.g");
			stream.CopyTo (ms);
			stream = assembly.GetManifestResourceStream ("SpaceInvaders.invaders.f");
			stream.CopyTo (ms);
			stream = assembly.GetManifestResourceStream ("SpaceInvaders.invaders.e");
			stream.CopyTo (ms);
			return ms;
		}

		void IVideoDevice.vblank ()
		{
			var ram = system.getVram ();
		}
	}
}

