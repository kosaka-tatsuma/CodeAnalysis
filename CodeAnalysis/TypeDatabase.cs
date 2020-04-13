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
		readonly List<TypeSymbolContainer> _TypeSymbolListInternal = new List<TypeSymbolContainer>();
		public readonly ReadOnlyCollection<TypeSymbolContainer> _TypeSymbolList;

		public TypeDatabase()
		{
			_TypeSymbolList = new ReadOnlyCollection<TypeSymbolContainer>(_TypeSymbolListInternal);
		}

		public void Build(ProjectAnalyzer analyzer)
		{
			using(var ws = analyzer.GetWorkspace()) {
				foreach(var proj in ws.CurrentSolution.Projects) {
					var comp = proj.GetCompilationAsync().Result;
					foreach(var cls_symbol in comp.GetSymbolsWithName(_ => true, SymbolFilter.Type).OfType<ITypeSymbol>()) {
						var container = new TypeSymbolContainer(cls_symbol);
						_TypeSymbolListInternal.Add(container);
					}

					var walker = new Walker();
					foreach(var doc in proj.Documents) {
						Console.WriteLine(doc.FilePath);
						if(System.IO.Path.HasExtension(".cs") && doc.TryGetSyntaxRoot(out var root)) {
							walker.Visit(root);
						}
						Console.WriteLine("--- End");
					}
				}
			}
		}
		/*
		public string DumpSyntaxNode(SyntaxNode node)
		{
			string core(SyntaxNode tmp_node, int depth = 0)
			{
				var ret = tmp_node.RawKind

				foreach(var )
			}

			return core(node);
		}*/

		class Walker : SyntaxWalker
		{
			public override void Visit(SyntaxNode node)
			{
				//if(node is ICompilationUnitSyntax syntax)
				{
					Console.WriteLine($"[Type : {node.GetType()}");
				}
				
				base.Visit(node);
			}
		}
	}

	public abstract class SymbolContainer<T> where T : ISymbol
	{
		public readonly T _Symbol;

		public SymbolContainer(T symbol) => _Symbol = symbol;
	}

	public sealed class TypeSymbolContainer : SymbolContainer<ITypeSymbol>
	{
		readonly List<MethodSymbolContainer> _MethodSymbolContainerListInternal = new List<MethodSymbolContainer>();
		public readonly ReadOnlyCollection<MethodSymbolContainer> _MethodSymbolContainerList;

		public TypeSymbolContainer(ITypeSymbol symbol) : base(symbol)
		{
			_MethodSymbolContainerList = new ReadOnlyCollection<MethodSymbolContainer>(_MethodSymbolContainerListInternal);

			foreach(var member_symbol in symbol.GetMembers()) {
				switch(member_symbol.Kind) {
				case SymbolKind.Method: {
						var method_symbol = (IMethodSymbol)member_symbol;
						_MethodSymbolContainerListInternal.Add(new MethodSymbolContainer(method_symbol));
					}
					break;

				case SymbolKind.Property:
				case SymbolKind.Field:
				case SymbolKind.NamedType:
				case SymbolKind.Event:
					break;

				default:
					throw new InvalidOperationException($"{member_symbol.Kind} is no support {nameof(SymbolKind)}.");
				}
			}
		}
	}

	public sealed class MethodSymbolContainer : SymbolContainer<IMethodSymbol>
	{
		public string ScriptBody { get; private set; }

		SyntaxNode SyntaxNode { get; set; }

		public MethodSymbolContainer(IMethodSymbol symbol) : base(symbol) { }

		public void Initialize(string body)
		{
			ScriptBody = body;
		}
	}
}