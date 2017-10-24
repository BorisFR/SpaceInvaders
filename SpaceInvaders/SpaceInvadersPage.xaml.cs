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
		//private const int CYCLES_PER_LOOP = 290000; // 16666; // 10000000;
		private const int WIDTH = 224;
		private const int HEIGHT = 256;

		private BmpMaker bmp;
		private ImageSource imageSource;
		DateTime lastCycle = DateTime.Now;
		DateTime timerFps = DateTime.Now;

		public SpaceInvadersPage ()
		{
			InitializeComponent ();
			btLaunch.Clicked += BtLaunch_Clicked;
			btStop.Clicked += BtStop_Clicked;
			btCoin.Clicked += BtCoin_Clicked;
			btP1Start.Clicked += BtP1Start_Clicked;
			//btP1Left.Clicked += BtP1Left_Clicked;
			//btP1Right.Clicked += BtP1Right_Clicked;
			btP1Shoot.Clicked += BtP1Shoot_Clicked;
			btP1Left.Pressed += (sender, e) => { Global.ButtonP1Left = true; };
			btP1Left.Released += (sender, e) => { Global.ButtonP1Left = false; };
			btP1Right.Pressed += (sender, e) => { Global.ButtonP1Right = true; };
			btP1Right.Released += (sender, e) => { Global.ButtonP1Right = false; };
			bmp = new BmpMaker (WIDTH, HEIGHT);

			for (int row = 0; row < HEIGHT; row++)
				for (int col = 0; col < WIDTH; col++) {
					bmp.SetPixel (row, col, 2 * row, 0, 2 * (HEIGHT - row));
				}
			bmp.SetPixel (100, 100, 255, 0, 0);
			imageSource = bmp.Generate ();
			theImage.Source = imageSource;
			btStop.IsEnabled = false;
			theMarquee.Source = ImageSource.FromResource ("SpaceInvaders.marquee.jpg");
		}

		protected override void OnSizeAllocated (double width, double height)
		{
			base.OnSizeAllocated (width, height);
			// WidthRequest="1320" HeightRequest="1450" 
			if (width * height > 0) {
				if (width < height) {
					theImage.WidthRequest = width;
					theImage.HeightRequest = HEIGHT * width / WIDTH;
				} else {
					theImage.HeightRequest = height;
					theImage.WidthRequest = WIDTH * height / HEIGHT;
				}
			}
		}

		void BtLaunch_Clicked (object sender, EventArgs e)
		{
			btLaunch.IsEnabled = false;
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
			btStop.IsEnabled = true;
		}

		void BtStop_Clicked (object sender, EventArgs e)
		{
			btStop.IsEnabled = false;
			cts.Cancel ();
			btLaunch.IsEnabled = true;
		}

		void BtCoin_Clicked (object sender, EventArgs e)
		{
			Global.ButtonInsertCoin = true;
		}

		void BtP1Start_Clicked (object sender, EventArgs e)
		{
			Global.ButtonStartP1 = true;
		}

		void BtP1Left_Clicked (object sender, EventArgs e)
		{
			Global.ButtonP1Left = true;
		}

		void BtP1Right_Clicked (object sender, EventArgs e)
		{
			Global.ButtonP1Right = true;
		}

		void BtP1Shoot_Clicked (object sender, EventArgs e)
		{
			Global.ButtonP1Fire = true;
		}

		void Emu_OneScreen ()
		{

			imageSource = emu.bmp.Generate ();
			Device.BeginInvokeOnMainThread (() => {
				theImage.Source = imageSource;
				//lastCycle = DateTime.Now;
			});

		}

		private async void DoRun ()
		{
			emu = new Emulator ();
			emu.OneScreen += Emu_OneScreen;

			DateTime thisCycle;
			TimeSpan deltaTime = new TimeSpan ();
			int count = 100;
			DateTime timeInterrupt = DateTime.Now;

			int toWait = 0;
			const double MHz = 2.0; // fréquence du processeur en Mega Hertz
			const double KHz = MHz * 1000; // en Kilo Hertz
			const double Hz = KHz * 1000; // en Hertz
										  //const double FrequencePerSecond = 1 / Hz; // soit le nombre de cycles par seconde
										  //const double FrequencePerMilliSec = 1000 / Hz; // nombre de cycles par milli-seconde

			// cf https://en.wikipedia.org/wiki/Instructions_per_secondMIPS
			const double INSTRUCTIONS_PER_CLOCK_CYCLE = 0.145;
			const double CYCLES_PER_LOOP = INSTRUCTIONS_PER_CLOCK_CYCLE * Hz; // 290000;


			const int millisSec = 60;
			const int second = 1000;
			const double frequency = second / millisSec;
			const int instructionsPerFrequency = (int)(CYCLES_PER_LOOP / frequency); // * millisSec / second;
																					 //const int instructionsPerFrequency = (int)(CYCLES_PER_LOOP);

			System.Diagnostics.Debug.WriteLine ($"Frequency: {millisSec} ms");
			System.Diagnostics.Debug.WriteLine ($"IPF: {instructionsPerFrequency}");

			lastCycle = DateTime.Now;

			while (true) {
				//emu.FetchExecute (CYCLES_PER_LOOP);
				emu.Execute (instructionsPerFrequency);

				// do we have to stop?
				if (cts != null) {
					if (cts.IsCancellationRequested) {
						emu = null;
						cts = null;
						System.Diagnostics.Debug.WriteLine ("Exit");
						return;
					}
				}

				// refresh display
				imageSource = emu.bmp.Generate ();
				Device.BeginInvokeOnMainThread (() => {
					theImage.Source = imageSource;
					count++;
				});

				// calculate elaps time
				thisCycle = DateTime.Now;
				deltaTime = thisCycle - lastCycle;
				lastCycle = thisCycle;

				// calculate the slowdown
				if (deltaTime.TotalMilliseconds < millisSec) {
					toWait = (millisSec - (int)deltaTime.TotalMilliseconds) / 10;
				} else toWait = 0;

				// display FPS
				if ((thisCycle - timerFps).TotalMilliseconds > 1000) {
					/*
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
						*/
					//System.Diagnostics.Debug.WriteLine (string.Format ("Running at ~{0:N2} MHz ({1} fps - wait {2})", count * 33.333 / (deltaTime).TotalMilliseconds, count, toWait));
					System.Diagnostics.Debug.WriteLine (string.Format ("Running @ ~{0:N2} MHz / {1} fps / wait {2}", (count / INSTRUCTIONS_PER_CLOCK_CYCLE) / (deltaTime).TotalMilliseconds / 100, count, toWait));
					//System.Diagnostics.Debug.WriteLine (string.Format ("{0} fps - wait {1}", count, toWait));
					timerFps = thisCycle;
					count = 0;
				}

				// do we have to slowdown?
				if (toWait > 0)
					await Task.Delay (toWait);

				//lastCycle = DateTime.Now;

			} // while
		}



	}
}