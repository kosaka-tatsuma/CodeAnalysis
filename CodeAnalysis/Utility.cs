using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeAnalysis
{
	using System.Diagnostics;
	using Microsoft.CodeAnalysis;

	public static class Util
	{
		#region Diagnostics
		public static void Assert(bool condition, string msg) => Trace.Assert(condition, msg);

		public static void InfoLine(string msg) => Trace.TraceInformation(msg);

		public static void TraceLine(string msg) => Trace.WriteLine(msg);

		public static void ErrorLine(string msg) => Trace.TraceError(msg);
		#endregion
	}

	public static class SymbolExtension
	{
		public static string GetFqn(this ISymbol self) => self.ContainingNamespace.Name + "." + self.ContainingType.Name + "." + self.Name;
	}
}
