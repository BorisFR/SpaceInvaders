using System;

// https://code.google.com/archive/p/cogwheel/

namespace Z80
{
	public partial class Z80
	{
		public Z80 ()
		{
			InitialiseTables ();
			Reset ();
		}

		public virtual void Reset ()
		{
			this.ResetRegisters ();
			this.ResetInterrupts ();
			this.PendingCycles = 0;
			this.ExpectedExecutedCycles = 0;
			this.TotalExecutedCycles = 0;
		}

	}
}