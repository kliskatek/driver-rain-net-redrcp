using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kliskatek.REDRCP
{
    internal class Constants
    {
        public const byte Preamble = 0xBB;
        public const byte EndMark = 0x7E;
        public const int DetailReaderInfoRegionOffset = 1;
        public const int DetailReaderInfoMinTxPowerOffset = 16;
        public const int DetailReaderInfoMaxTxPowerOffset = 18;
    }
}
