using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CodeAnalysis
{
	class Program
	{
		static Program _Instance = new Program();

		static void Main(string[] args)
		{
			var manager = new Buildalyzer.AnalyzerManager(@"C:\Users\Kosaka\Documents\GitHub\BatEn\BatEn.sln");

			foreach(var proj in manager.Projects) {
				var typedb = new TypeDatabase();
				typedb.Build(proj.Value);

				foreach(var type_symbol in typedb._TypeSymbolList) {
					foreach(var member in type_symbol.GetMembers()) {
						if(member.Kind == SymbolKind.Method && member is IMethodSymbol method_symbol) {
							Console.WriteLine(method_symbol.ToDisplayString());
						}
					}
				}
			}

			Console.WriteLine("Fin");
			Console.ReadKey();
		}


	}
}
