using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Cobra.Communication.SPI
{
	public abstract class CInterfaceSPI : CInterfaceBase
	{
		#region Public Member Declaration

		public UInt32 SPIBaudRate { get; set; }
		public UInt32 SPIConfigure { get; set; }

		#endregion
	}
}
