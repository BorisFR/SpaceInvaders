﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace eZet.i8080.Emulator {
    public interface IOutputDevice {

        void write(ushort port, Byte data);
    }
}
