using System;
using System.Threading;
using System.Threading.Tasks;
using Xamarin.Forms;
using System.Runtime.Serialization;

namespace SpaceInvaders
{
	public partial class SpaceInvadersPage : ContentPage
	{
		Action action;
		CancellationTokenSource cts;
		Task task;
		Emulator emu;
		//Emu8080 emu;
		private const int CYCLES_PER_LOOP = 16666; // 10000000;
		private const int WIDTH = 224;
		private const int HEIGHT = 256;

		private BmpMaker bmp;
		private ImageSource imageSource;
		DateTime lastCycle = DateTime.Now;


		public SpaceInvadersPage ()
		{
			InitializeComponent ();
			btLaunch.Clicked += BtLaunch_Clicked;
			bmp = new BmpMaker (WIDTH, HEIGHT);

			for (int row = 0; row < HEIGHT; row++)
				for (int col = 0; col < WIDTH; col++) {
					bmp.SetPixel (row, col, 2 * row, 0, 2 * (HEIGHT - row));
				}
			bmp.SetPixel (100, 100, 255, 0, 0);
			imageSource = bmp.Generate ();
			theImage.Source = imageSource;
		}

		void Emu_OneScreen ()
		{

			imageSource = emu.bmp.Generate ();
			Device.BeginInvokeOnMainThread (() => {
				theImage.Source = imageSource;
				lastCycle = DateTime.Now;
			});

		}

		private async void DoRun ()
		{
			emu = new Emulator ();
			emu.OneScreen += Emu_OneScreen;

			DateTime thisCycle;
			TimeSpan deltaTime = new TimeSpan ();
			int count = 0;
			DateTime timeInterrupt = DateTime.Now;

			int toWait = 0;
			double mhz = 2;
			const double MHZ = 3.5;

			while (true) {
				//emu.Execute (17000);
				//emu.Execute (16333);
				emu.Execute (17066);
				//emu.Execute (17066);
				//thisCycle = DateTime.Now;

				//deltaTime = thisCycle - timeInterrupt;
				//if (deltaTime.TotalMilliseconds > 8) {
				//timeInterrupt = thisCycle;
				/*
				if (!emu.NonMaskableInterruptPending && !emu.Interrupt) {
					interruptType = 3 - interruptType;
					emu.InterruptMode = interruptType;
					emu.Interrupt = true;
				}
				*/
				//}
				//emu.FetchExecute (CYCLES_PER_LOOP);

				thisCycle = DateTime.Now;
				deltaTime = thisCycle - lastCycle;
				count++;

				imageSource = emu.bmp.Generate ();
				Device.BeginInvokeOnMainThread (() => {
					theImage.Source = imageSource;
				});

				if (cts != null) {
					if (cts.IsCancellationRequested) {
						emu = null;
						System.Diagnostics.Debug.WriteLine ("Exit");
						return;
					}
				}
				if (deltaTime.TotalMilliseconds > 1000) {
					mhz = count * 33.333 / (deltaTime).TotalMilliseconds;
					if (mhz > MHZ) {
						if (mhz - MHZ > 1000)
							toWait += 1000;
						else if (mhz - MHZ > 100)
							toWait += 100;
						else if (mhz - MHZ > 10)
							toWait += 10;
						else
							toWait++;
					}
					if (mhz < MHZ)
						toWait--;
					System.Diagnostics.Debug.WriteLine (string.Format ("Running at ~{0:N2} MHz ({1} fps - wait {2})", count * 33.333 / (deltaTime).TotalMilliseconds, count, toWait));
					lastCycle = thisCycle;
					count = 0;

				}

				if (toWait > 0)
					await Task.Delay (toWait);

			} // while
		}


		void BtLaunch_Clicked (object sender, EventArgs e)
		{
			if (task != null) {
				if (!task.IsCompleted) {
					cts.Cancel ();
					return;
				}
				cts = null;
			}
			cts = new CancellationTokenSource ();
			action = new Action (DoRun);
			task = new Task (action, cts.Token);
			task.Start ();
		}

	}
}