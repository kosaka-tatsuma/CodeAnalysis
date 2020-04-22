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
				var typedb = new TypeDatabase();
				Console.WriteLine("Building " + proj.Key + " now...");
				typedb.Build(proj.Value);

				var checker = new AnalysisChecker();
				using(var writer = new System.IO.StreamWriter($"result_{proj.Value.ProjectInSolution.ProjectName}.csv")) {
					foreach(var type_symbol in typedb._TypeSymbolList) {
						Console.WriteLine("==== " + type_symbol._Symbol.Name + " - " + (type_symbol._Symbol.IsValueType ? "struct" : "class") + "====");
						foreach(var container in type_symbol._MethodSymbolContainerList) {
							Console.WriteLine(container._Symbol.Name);
							if(container.Valid) {
								var results = checker.Analysis(container);
								foreach(var result in results) {
									writer.WriteLine(result.DumpCsv());
								}
							}
						}
					}
				}
			}

			Console.WriteLine("Fin");
		}
	}

	public class AnalysisChecker: CSharpSyntaxWalker
	{
		public const int _LanguageLayer = 0;

		public virtual List<AnalysisResultBase> Analysis(MethodSymbolContainer container)
		{
			var results = new List<AnalysisResultBase>();

			if(CheckWaistMethodOverride(container, out var result)) {
				results.Add(result);
			}

			var variable_dict = new Dictionary<string, TypeSyntax>();
			//foreach(var param in container._Symbol.Parameters) {
			//	variable_dict.Add(param.Name, (TypeSyntax)param.Type.DeclaringSyntaxReferences[0].GetSyntax());
			//}
			//
			//VisitStatement(variable_dict, container._Block, (dict, statement) => {
			//
			//});

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

		IEnumerable<AnalysisResultBase> CheckNullCheckMisstake(BlockSyntax block)
		{
			yield break;
		}

		void VisitStatement(Dictionary<string, TypeSyntax> variable_dict, StatementSyntax statement, Action<Dictionary<string, TypeSyntax>, StatementSyntax> action)
		{
			var childlen = new SyntaxList<StatementSyntax>();

			var scope_variable_list = new List<string>();
			switch(statement.Kind()) {
			case SyntaxKind.Block: {
					var block = (BlockSyntax)statement;
					childlen = block.Statements;
				}
				break;

			case SyntaxKind.IfStatement: {
					var if_statement = (IfStatementSyntax)statement;
					childlen = childlen.Add(if_statement.Statement);
				}
				break;

			case SyntaxKind.SwitchStatement: {
					var sw_statement = (SwitchStatementSyntax)statement;
					foreach(var section in sw_statement.Sections) {
						childlen = childlen.AddRange(section.Statements);
					}
				}
				break;

			case SyntaxKind.LocalDeclarationStatement: {
					var ldec_statement = (LocalDeclarationStatementSyntax)statement;
					foreach(var declarator in ldec_statement.Declaration.Variables) {
						variable_dict.Add(declarator.Identifier.Text, ldec_statement.Declaration.Type);
					}
				}
				break;
			}

			foreach(var child in childlen) {
				action(variable_dict, child);
				VisitStatement(variable_dict, child, action);
			}
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

	public class Variable
	{
		public string Name { get; }

		public ITypeSymbol Type { get; }

		public Variable(string name, ITypeSymbol type)
		{
			Name = name;
			Type = type;
		}
	}

	public enum Code
	{
		WaistOverride,
		MissingNullcheck,
		Max,
	}
}