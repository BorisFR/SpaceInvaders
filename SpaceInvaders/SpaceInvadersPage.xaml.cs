using System;
using System.Threading;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace SpaceInvaders
{
	public partial class SpaceInvadersPage : ContentPage
	{
		Action action;
		CancellationTokenSource cts;
		Task task;
		Emulator emu;
		private const int CYCLES_PER_LOOP = 10000000;
		private const int WIDTH = 224;
		private const int HEIGHT = 256;

		private BmpMaker bmp;
		private ImageSource imageSource;

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
			//theImage.Source = ImageSource.FromResource ("SpaceInvaders.peace.jpg");
		}

		private void DoRun ()
		{
			emu = new Emulator ();

			DateTime lastCycle = DateTime.Now;
			DateTime thisCycle;
			TimeSpan deltaTime = new TimeSpan ();
			int count = 0;
			int interruptType = 0;
			DateTime timeInterrupt = DateTime.Now;
			emu.InterruptMode = 1;

			while (true) {
				emu.FetchExecute (CYCLES_PER_LOOP);
				thisCycle = DateTime.Now;

				deltaTime = thisCycle - timeInterrupt;
				if (deltaTime.TotalMilliseconds > 100) {
					timeInterrupt = thisCycle;
					//if (!emu.Interrupt) {
					switch (interruptType) {
					case 0:
						//emu.InterruptMode = 2;
						//emu.Interrupt = true;
						//if (!emu.Interrupt) {
						if (!emu.NonMaskableInterruptPending) {
							emu.NonMaskableInterrupt = true;
							interruptType = 1 - interruptType;
							emu.IFF1 = true;
							//emu.Interrupt = true;
						}
						//}
						break;
					case 1:
						//emu.InterruptMode = 1;
						//emu.Interrupt = true;
						//emu.NonMaskableInterrupt = false;
						//if (!emu.Interrupt) {
						if (!emu.NonMaskableInterruptPending) {
							interruptType = 1 - interruptType;
							emu.IFF1 = true;
							//emu.NonMaskableInterrupt = false;
							//emu.Interrupt = true;
							//}
						}
						break;
						//}
					}
				}

				deltaTime = thisCycle - lastCycle;
				count++;
				if (deltaTime.TotalMilliseconds > 500) {
					//System.Diagnostics.Debug.WriteLine (string.Format ("Running at ~{0:N2} MHz", (CyclesPerLoop / 1000d) / (ThisCycle - LastCycle).TotalMilliseconds));
					System.Diagnostics.Debug.WriteLine (string.Format ("Running at ~{0:N2} MHz", count * (CYCLES_PER_LOOP / 1000d) / (deltaTime).TotalMilliseconds));
					lastCycle = thisCycle;
					count = 0;
					imageSource = emu.bmp.Generate ();
					Device.BeginInvokeOnMainThread (() => {
						theImage.Source = imageSource;
					});
				}
				if (cts != null) {
					if (cts.IsCancellationRequested)
						return;
				}
			}
			System.Diagnostics.Debug.WriteLine ("Exit");
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