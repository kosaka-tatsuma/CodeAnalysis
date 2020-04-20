using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;

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

			var walkers = new AnalysisWalker[] {
				new Language.CallOnlyBaseWalker(),
			};

			foreach(var proj in manager.Projects) {
				var typedb = new TypeDatabase();
				Console.WriteLine("Building " + proj.Key + " now...");
				typedb.Build(proj.Value);

				using(var writer = new System.IO.StreamWriter($"result_{proj.Value.ProjectInSolution.ProjectName}.csv")) {
					foreach(var type_symbol in typedb._TypeSymbolList) {
						Console.WriteLine("==== " + type_symbol._Symbol.Name + " - " + (type_symbol._Symbol.IsValueType ? "struct" : "class") + "====");
						foreach(var container in type_symbol._MethodSymbolContainerList) {
							Console.WriteLine(container._Symbol.Name);

							foreach(var walker in walkers) {
								if(walker.Analysis(container, out var list)) {
									foreach(var e in list) {
										writer.WriteLine(e.DumpCsv());
									}
								}
							}
						}
					}
				}
			}

			Console.WriteLine("Fin");
		}
	}

	abstract class AnalysisWalker: SyntaxWalker
	{
		public abstract bool Analysis(MethodSymbolContainer container, out List<AnalysisResultBase> results);
	}
}

namespace CodeAnalysis.Language
{
	class CallOnlyBaseWalker: AnalysisWalker
	{
		public override bool Analysis(MethodSymbolContainer container, out List<AnalysisResultBase> results)
		{
			results = null;
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

			results = new List<AnalysisResultBase>(1) {
				new AnalysisResult<Code>(container._Symbol, Define._Layer, (int)Code.WaistOverride) 
			};
			return true;
		}
	}

	public enum Code
	{
		WaistOverride,
		Max,
	}

	public static class Define
	{
		public static int _Layer = 0;

		static ReadOnlyDictionary<Code, string> CommentDict { get; } = new ReadOnlyDictionary<Code, string>(new Dictionary<Code, string>() {
			{ Code.WaistOverride, "不要なメソッドオーバーライドです。" },
		});

		public static ReadOnlyCollection<string> Comments { get; }

		static Define()
		{
			var arr = CommentDict
				.OrderBy(e => e.Key)
				.Select(e => e.Value)
				.ToArray();
			Comments = new ReadOnlyCollection<string>(arr);
		}
	}
}
