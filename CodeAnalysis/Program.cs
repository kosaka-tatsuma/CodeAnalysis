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
				Console.WriteLine("Building " + proj.Key + " now...");
				typedb.Build(proj.Value);

				foreach(var type_symbol in typedb._TypeSymbolList) {
					Console.WriteLine(type_symbol._Symbol.Name + " - " + (type_symbol._Symbol.IsValueType? "struct" : "class"));
					foreach(var symbol in type_symbol._MethodSymbolContainerList) {
						Console.WriteLine("\t" + symbol._Symbol.Name);
					}
				}

				proj.Value.Build();
			}

			Console.WriteLine("Fin");
			Console.ReadKey();
		}


	}
}
