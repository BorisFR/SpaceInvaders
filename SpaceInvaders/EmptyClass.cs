private int checkInterrupt (int cycle)
{
	if (NMI || (IFF0 && IRQ)) {
		if (NMI) { // Take NMI
				   //if (!goingToirq) {
				   //if (!debugDisabled) { log("...CPUZ80 takes non maskable interrupt"); }
			state_HALT = false;

			IFF1 = IFF0;
			NMI = IFF0 = false;

			push (PC);
			PC = 0x0066;                    // ...and jump to 0x0066

			cycle -= 13;
			//} else {
			//   goingToirq = false; /* CPU has to execute 1 more instruction */
			//}
		}

		if (IFF0 && IRQ) {  // Take interrupt if enabled
							//  System.out.println("...CPUZ80 takes interrupt using interrupt mode "+Integer.toString(IM));
			state_HALT = false;

			switch (IM) {
			case 0: // IM0  --> exec 1-byte instruction. Only calls are supported.
				IRQ = IFF0 = false;
				push (PC);
				if (I_Vector == 0 || I_Vector == 255) { PC = 0x0038; } else { PC = I_Vector; }
				cycle -= 13;
				break;
			case 1: // IM1	--> RST &38
				IRQ = IFF0 = false;
				push (PC);
				PC = 0x0038;
				cycle -= 13; // RST &38 = 11 cycles    + 2 cycles
				break;
			case 2: // IM2  --> Call I:Vector
				IRQ = IFF0 = false;
				push (PC);
				PC = peekw ((I << 8) | I_Vector);
				cycle = cycle - 19; // Call = 17 cycles    + 2 cycles
				break;
			default:
				break;
			}
			//} else {
			//	goingToirq = false; // CPU has to execute 1 more instruction
			//}
		}
	}
	return cycle;
}
