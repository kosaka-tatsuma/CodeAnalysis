using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeAnalysis
{
	using System.Diagnostics;

	public static class util
	{
		#region Diagnostics
		public static void Assert(bool condition, string msg) => Trace.Assert(condition, msg);

		public static void InfoLine(string msg) => Trace.TraceInformation(msg);

		public static void TraceLine(string msg) => Trace.WriteLine(msg);

		public static void ErrorLine(string msg) => Trace.TraceError(msg);
		#endregion
	}
}
