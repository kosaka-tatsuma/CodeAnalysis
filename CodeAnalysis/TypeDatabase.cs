using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

using Microsoft.CodeAnalysis;

using Buildalyzer;
using Buildalyzer.Workspaces;

namespace CodeAnalysis
{
	public sealed class TypeDatabase
	{
		readonly List<ITypeSymbol> _TypeSymbolListInternal = new List<ITypeSymbol>();

		public readonly ReadOnlyCollection<ITypeSymbol> _TypeSymbolList;

		public TypeDatabase()
		{
			_TypeSymbolList = new ReadOnlyCollection<ITypeSymbol>(_TypeSymbolListInternal);
		}

		public void Build(ProjectAnalyzer analyzer)
		{
			using(var ws = analyzer.GetWorkspace()) {
				foreach(var proj in ws.CurrentSolution.Projects) {
					var comp = proj.GetCompilationAsync().Result;
					foreach(var cls_symbol in comp.GetSymbolsWithName(_ => true, SymbolFilter.Type).OfType<ITypeSymbol>()) {
						_TypeSymbolListInternal.Add(cls_symbol);
					}
				}
			}

			foreach(var type_symbol in _TypeSymbolListInternal) {
				Console.WriteLine(type_symbol.Name);
				foreach(var member_symbol in type_symbol.GetMembers()) {
					Console.WriteLine($"\t{member_symbol.Name} - {member_symbol.Kind}");
				}
			}
		}
	}
}
