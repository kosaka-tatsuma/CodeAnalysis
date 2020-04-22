using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeAnalysis
{
	using Microsoft.CodeAnalysis;

	public abstract class AnalysisResultBase
	{
		public int Layer { get; } // 0は言語層

		public string Fqn { get; }

		public object[] Parameters { get; }

		public AnalysisResultBase(IMethodSymbol symbol, int layer, object[] param)
		{
			Layer = layer;
			Fqn = symbol.ToString();
			Parameters = param;
		}

		public abstract string DumpCsv();
	}

	public class AnalysisResult<T>: AnalysisResultBase where T : Enum
	{
		public T Code { get; }

		public AnalysisResult(IMethodSymbol symbol, int layer, T code, params object[] param) : base(symbol, layer, param)
		{
			Code = code;
		}

		public override string DumpCsv()
		{
			var dump = string.Join(",", new string[] { Layer.ToString(), Code.ToString(), Fqn });
			if(Parameters.Length > 0) {
				dump = string.Concat(dump, ",", string.Join(",", Parameters));
			}
			return dump;
		}
	}
}
