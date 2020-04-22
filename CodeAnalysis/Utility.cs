using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
		class AssertException : Exception
		{
			public AssertException(string msg) : base(msg) { }
		}

		public static void Assert(bool condition, string msg)
		{
			if(condition == false) {
				throw new AssertException(msg);
			}
		}

		public static void InfoLine(string msg) => Trace.TraceInformation(msg);

		public static void TraceLine(string msg) => Trace.WriteLine(msg);

		public static void ErrorLine(string msg) => Trace.TraceError(msg);
		#endregion

		#region IList&IEnumerable
		public static ReadOnlyCollection<T> ToReadOnlyCollection<T>(this IList<T> self) => new ReadOnlyCollection<T>(self);
		#endregion
	}

	public static class SymbolExtension
	{
		public static string GetFqn(this ISymbol self) => self.ContainingNamespace.Name + "." + self.ContainingType.Name + "." + self.Name;
	}
}
