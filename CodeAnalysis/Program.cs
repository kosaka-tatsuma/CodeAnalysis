using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeAnalysis
{
	class Program
	{
		static Program _Instance = new Program();

		static ReadOnlyDictionary<Code, string> CommentDict { get; } = new ReadOnlyDictionary<Code, string>(new Dictionary<Code, string>() {
			{ Code.WaistOverride, "不要なメソッドオーバーライドです。" },
		});

		public static ReadOnlyCollection<string> Comments { get; }

		static Program()
		{
			var arr = CommentDict
				.OrderBy(e => e.Key)
				.Select(e => e.Value)
				.ToArray();
			Comments = new ReadOnlyCollection<string>(arr);
		}

		static void Main(string[] args)
		{
			var manager = new Buildalyzer.AnalyzerManager(@"C:\Users\Kosaka\Documents\GitHub\BatEn\BatEn.sln");

			foreach(var proj in manager.Projects) {
				Util.InfoLine("Building " + proj.Key + " now...");
				var checker = new AnalysisChecker();
				checker.Analysis(proj.Value);
			}

			Util.InfoLine("Fin");
		}
	}

	public class AnalysisChecker: CSharpSyntaxWalker
	{
		public const int _LanguageLayer = 0;

		readonly TypeDatabase _TypeDatabase = new TypeDatabase();

		public void Analysis(Buildalyzer.ProjectAnalyzer analyzer)
		{
			_TypeDatabase.Build(analyzer);

			using(var writer = new System.IO.StreamWriter($"result_{analyzer.ProjectInSolution.ProjectName}.csv")) {
				foreach(var type_symbol in _TypeDatabase._TypeSymbolList) {
					//Console.WriteLine("==== " + type_symbol._Symbol.Name + " - " + (type_symbol._Symbol.IsValueType ? "struct" : "class") + "====");
					foreach(var container in type_symbol._MethodSymbolContainerList) {
						if(container.Valid == false) {
							continue;
						}
						var results = Analysis(container);
						foreach(var result in results) {
							writer.WriteLine(result.DumpCsv());
						}
					}
				}
			}
		}

		public virtual List<AnalysisResultBase> Analysis(MethodSymbolContainer container)
		{
			var results = new List<AnalysisResultBase>();

			if(CheckWaistMethodOverride(container, out var result)) {
				results.Add(result);
			}

			container.VisitStatementInMethodBlock((dict, statement) => {
				
			});

			return results;
		}

		bool CheckWaistMethodOverride(MethodSymbolContainer container, out AnalysisResult<Code> result)
		{
			result = null;
			if(container._Symbol.IsOverride == false) {
				return false;
			}

			var root = container._Block;
			if(root.ChildNodes().Count() != 1) {
				return false;
			}

			var base_nodes = root.DescendantNodes().Where(e => e.Kind() == SyntaxKind.BaseExpression).ToArray();
			if(base_nodes.Length != 1) {
				return false;
			}

			result = new AnalysisResult<Code>(container._Symbol, _LanguageLayer, Code.WaistOverride);
			return true;
		}

		protected IEnumerable<SyntaxNode> EnumerateNodes(SyntaxNode root)
		{
			yield return root;

			foreach(var child in root.ChildNodes()) {
				foreach(var descend in EnumerateNodes(child)) {
					yield return descend;
				}
			}
		}
	}

	public enum Code
	{
		WaistOverride,
		MissingNullcheck,
		Max,
	}
}