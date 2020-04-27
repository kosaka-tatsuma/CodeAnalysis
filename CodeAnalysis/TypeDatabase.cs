using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using Buildalyzer;
using Buildalyzer.Workspaces;

namespace CodeAnalysis
{
	using Microsoft.CodeAnalysis;
	using Microsoft.CodeAnalysis.CSharp;
	using Microsoft.CodeAnalysis.CSharp.Syntax;

	public sealed class TypeDatabase
	{
		static readonly string FootprintFolderName = "Footprint";

		readonly List<TypeSymbolContainer> _TypeSymbolListInternal = new List<TypeSymbolContainer>();
		public readonly ReadOnlyCollection<TypeSymbolContainer> _TypeSymbolList;

		Compilation Compilation { get; set; }

		public TypeDatabase()
		{
			_TypeSymbolList = new ReadOnlyCollection<TypeSymbolContainer>(_TypeSymbolListInternal);
		}

		public void Build(ProjectAnalyzer analyzer)
		{
			using(var ws = analyzer.GetWorkspace()) {
				var projs = ws.CurrentSolution.Projects.ToArray();
				Util.Assert(projs.Length == 1, "Projectが複数存在することは想定されていません。");
				var proj = projs[0];

				//--- ログの蓄積フォルダの生成 ---//
				var dirinfo = Directory.CreateDirectory(FootprintFolderName + "\\" + proj.Name);

				//--- TypeSymbolの収集 ---//
				var comp = Compilation = proj.GetCompilationAsync().Result;
				_TypeSymbolListInternal.Clear();
				foreach(var cls_symbol in comp.GetSymbolsWithName(_ => true, SymbolFilter.Type).OfType<ITypeSymbol>()) {
					var container = new TypeSymbolContainer(this, cls_symbol);
					_TypeSymbolListInternal.Add(container);
				}

				//--- スクリプトファイルの中身をダンプ ---//
				foreach(var doc in proj.Documents) {
					using(var writer = new StreamWriter(dirinfo.FullName +  $"\\footprint_{doc.Name.Replace(".", "_")}_{doc.Id.Id}.log")) {
						var walker = new Walker() { Writer = writer };
						writer.WriteLine(doc.FilePath);
						if(Path.GetExtension(doc.FilePath) == ".cs" && doc.TryGetSyntaxRoot(out var root)) {
							walker.Visit(root);
						}
						writer.WriteLine("--- End");
					}
				}
			}
		}
		
		public ISymbol FindSymbol(CSharpSyntaxNode syntax)
		{
			try {
				var model = Compilation.GetSemanticModel(syntax.SyntaxTree);
				var symbol = model.GetDeclaredSymbol(syntax);
				return symbol;
			}
			catch {
				Util.ErrorLine(syntax.ToString() + " is not found...");
				return null;
			}
		}

		class Walker : SyntaxWalker
		{
			public TextWriter Writer { get; set; }

			public readonly List<MethodDeclarationSyntax> _MethodList = new List<MethodDeclarationSyntax>();

			public override void Visit(SyntaxNode node)
			{
				var parent_kind = node.Parent?.Kind() ?? SyntaxKind.None;

				var writer = Writer;
				writer?.WriteLine($"[Kind : {node.Kind()}][Parent : {parent_kind}][Type : {node.GetType()}]");

				switch(node.Kind()) {
				case SyntaxKind.UsingDirective: {
						if(writer != null) {
							var directive = (UsingDirectiveSyntax)node;
							writer.WriteLine(nameof(directive.Name) + " : " + directive.Name);
						}
					}
					break;

				case SyntaxKind.NamespaceDeclaration: {
						if(writer != null) {
							var declaration = (NamespaceDeclarationSyntax)node;
							writer.WriteLine(nameof(declaration.Name) + " : " + DumpTypeSyntax(declaration.Name));
							if(DumpMemberDeclaration(declaration, out var msg)) {
								writer.WriteLine(msg);
							}
						}
					}
					break;

				case SyntaxKind.ClassDeclaration: {
						if(writer != null) {
							var declaration = (ClassDeclarationSyntax)node;
							writer.WriteLine(nameof(declaration.Identifier) + " : " + declaration.Identifier.Text);
							if(declaration.BaseList != null) {
								var count = 0;
								foreach(var base_class in declaration.BaseList.Types) {
									writer.WriteLine($"BaseType{count++:D2} : " + DumpTypeSyntax(base_class.Type));
								}
							}
							if(DumpMemberDeclaration(declaration, out var msg)) {
								writer.WriteLine(msg);
							}
						}
					}
					break;

				case SyntaxKind.MethodDeclaration: {
						var declaration = (MethodDeclarationSyntax)node;
						_MethodList.Add(declaration);

						if(writer != null) {
							writer.WriteLine(nameof(declaration.Identifier) + " : " + declaration.Identifier.Text);
							writer.WriteLine(nameof(declaration.ReturnType) + " : " + DumpTypeSyntax(declaration.ReturnType));
							var count = 0;
							foreach(var param in declaration.ParameterList.Parameters) {
								writer.WriteLine($"Param{count++:D2} : " + DumpTypeSyntax(param.Type) + $"({param.Type.GetType()}) - " + param.Identifier.Text);
							}
							if(DumpMemberDeclaration(declaration, out var msg)) {
								writer.WriteLine(msg);
							}
						}
					}
					break;

				case SyntaxKind.FieldDeclaration: {
						if(writer != null) {
							var declaration = (FieldDeclarationSyntax)node;
							writer.WriteLine(DumpVariableDeclaration(declaration.Declaration));
							if(DumpMemberDeclaration(declaration, out var msg)) {
								writer.WriteLine(msg);
							}
						}
					}
					goto default;

				case SyntaxKind.PropertyDeclaration: {
						if(writer != null) {
							var declaration = (PropertyDeclarationSyntax)node;
							if(writer != null) {
								writer.WriteLine("Type : " + DumpTypeSyntax(declaration.Type));
								if(DumpMemberDeclaration(declaration, out var msg)) {
									writer.WriteLine(msg);
								}
								if(declaration.AccessorList != null) {
									var count = 0;
									foreach(var accessor in declaration.AccessorList.Accessors) {
										writer.WriteLine($"--- Accessor{count++:D2} ---");
										writer.WriteLine("Keyword : " + accessor.Keyword.Text);
										if(DumpAttributes(accessor.AttributeLists, out msg)) {
											writer.WriteLine(msg);
										}
										if(DumpModifiers(accessor.Modifiers, out msg)) {
											writer.WriteLine(msg);
										}
									}
								}
							}
						}
					}
					goto default;

				case SyntaxKind.ParameterList:
				case SyntaxKind.Parameter:
					if(node.FirstAncestorOrSelf<MethodDeclarationSyntax>() != null) {
						break;
					}
					goto default;

				case SyntaxKind.CompilationUnit:
				case SyntaxKind.BaseList:
				case SyntaxKind.AttributeList:
				case SyntaxKind.Attribute:
				case SyntaxKind.QualifiedName:
				case SyntaxKind.IdentifierName:
				case SyntaxKind.PredefinedType:
				case SyntaxKind.SimpleBaseType:
				case SyntaxKind.TupleElement:
				case SyntaxKind.TupleType:
					/*nop*/
					break;

				default:
					writer?.WriteLine(node.GetText());
					break;
				}

				base.Visit(node);
			}

			string DumpTypeSyntax(TypeSyntax syntax) => syntax.GetFqn();

			bool DumpMemberDeclaration(MemberDeclarationSyntax syntax, out string msg)
			{
				msg = "";
				var dirty = false;
				if(DumpAttributes(syntax.AttributeLists, out var tmp)) {
					msg += tmp;
					dirty = true;
				}
				if(DumpModifiers(syntax.Modifiers, out tmp)) {
					if(dirty) {
						msg += "\n";
					}
					msg += tmp;
					dirty = true;
				}
				return dirty;
			}

			bool DumpAttributes(SyntaxList<AttributeListSyntax> attr_lists, out string msg)
			{
				msg = "";
				var count = 0;
				foreach(var attr_list in attr_lists) {
					foreach(var attr in attr_list.Attributes) {
						if(count > 0) {
							msg += "\n";
						}
						msg += $"Attribute{count++:D2} : " + attr.Name;
					}
				}
				return count > 0;
			}

			bool DumpModifiers(SyntaxTokenList token_list, out string msg)
			{
				msg = "";
				var count = 0;
				foreach(var token in token_list) {
					if(count > 0) {
						msg += "\n";
					}
					msg += $"Modifier{count++:D2} : " + token.Kind() + $"({token.Text})";
				}
				return count > 0;
			}

			string DumpVariableDeclaration(VariableDeclarationSyntax syntax)
			{
				var msg = "Type : " + DumpTypeSyntax(syntax.Type);
				var count = 0;
				foreach(var vari in syntax.Variables) {
					msg += $"\nVariable{count++:D2} : " + vari.Identifier.Text;
				}
				return msg;
			}

			public void Clear()
			{
				_MethodList.Clear();
			}
		}
	}

	public abstract class SymbolContainer<T> where T : ISymbol
	{
		public readonly T _Symbol;

		protected readonly TypeDatabase _TypeDatabase;

		public SymbolContainer(TypeDatabase db, T symbol) => (_TypeDatabase, _Symbol) = (db, symbol);

		public override string ToString() => _Symbol.Name;
	}

	public sealed class TypeSymbolContainer : SymbolContainer<ITypeSymbol>
	{
		readonly List<MethodSymbolContainer> _MethodSymbolContainerListInternal = new List<MethodSymbolContainer>();
		public readonly ReadOnlyCollection<MethodSymbolContainer> _MethodSymbolContainerList;

		public TypeSymbolContainer(TypeDatabase db, ITypeSymbol symbol) : base(db, symbol)
		{
			_MethodSymbolContainerList = new ReadOnlyCollection<MethodSymbolContainer>(_MethodSymbolContainerListInternal);

			foreach(var member_symbol in symbol.GetMembers()) {
				switch(member_symbol.Kind) {
				case SymbolKind.Method: {
						_MethodSymbolContainerListInternal.Add(new MethodSymbolContainer(db, (IMethodSymbol)member_symbol));
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
		public bool Valid { get; }

		public readonly BlockSyntax _Block;

		readonly Dictionary<string, Variable> _VariableDict = new Dictionary<string, Variable>();

		public MethodSymbolContainer(TypeDatabase db, IMethodSymbol symbol) : base(db, symbol)
		{
			BlockSyntax create_block(ExpressionSyntax express_syntax)
			{
				StatementSyntax statement = null;
				if(_Symbol.ReturnsVoid) {
					statement = SyntaxFactory.ExpressionStatement(express_syntax);
				}
				else {
					statement = SyntaxFactory.ReturnStatement(express_syntax);
				}
				statement = statement.NormalizeWhitespace();
				return SyntaxFactory.Block(statement);
			}

			var block = SyntaxFactory.Block();
			if(_Symbol.DeclaringSyntaxReferences.Length > 0 && _Symbol.IsAbstract == false) {
				//--- 使用できる名前空間を検知 ---//
				var syntax = _Symbol.DeclaringSyntaxReferences[0].GetSyntax();

				//--- BlockSyntaxの設定 ---//
				switch(syntax.Kind()) {
				case SyntaxKind.MethodDeclaration: {
						var method = (MethodDeclarationSyntax)syntax;
						block = method.Body ?? create_block(method.ExpressionBody.Expression);
					}
					break;

				case SyntaxKind.ArrowExpressionClause: {
						var clause = (ArrowExpressionClauseSyntax)syntax;
						block = create_block(clause.Expression);
					}
					break;
				}

				Valid = true;
			}
			_Block = block;
		}

		public void VisitStatementInMethodBlock(Action<Dictionary<string, Variable>, StatementSyntax> action)
		{
			_VariableDict.Clear();
			foreach(var param in _Symbol.Parameters) {
				_VariableDict.Add(param.Name, new Variable(0, param));
			}

			VisitStatement(_VariableDict, _Block, action, 0);
		}

		void VisitStatement(Dictionary<string, Variable> variable_dict, StatementSyntax statement, Action<Dictionary<string, Variable>, StatementSyntax> action, int depth)
		{
			var childlen = new SyntaxList<StatementSyntax>();
			switch(statement.Kind()) {
			case SyntaxKind.Block: {
					var block = (BlockSyntax)statement;
					childlen = block.Statements;
				}
				break;

			case SyntaxKind.IfStatement: {
					var if_statement = (IfStatementSyntax)statement;
					childlen = childlen.Add(if_statement.Statement);
					VisitExpression(variable_dict, if_statement.Condition, depth);
				}
				break;

			case SyntaxKind.SwitchStatement: {
					var sw_statement = (SwitchStatementSyntax)statement;
					foreach(var section in sw_statement.Sections) {
						childlen = childlen.AddRange(section.Statements);
					}
				}
				break;

			case SyntaxKind.ForEachStatement: {
					var foreach_statement = (ForEachStatementSyntax)statement;
					childlen = childlen.Add(foreach_statement.Statement);
					var symbol = (ILocalSymbol)_TypeDatabase.FindSymbol(foreach_statement);
					if(symbol != null) {
						variable_dict.Add(symbol.Name, new Variable(depth, symbol));
					}
				}
				break;

			case SyntaxKind.LocalDeclarationStatement: {
					var ldec_statement = (LocalDeclarationStatementSyntax)statement;
					foreach(var declarator in ldec_statement.Declaration.Variables) {
						if(declarator.Initializer != null) {
							VisitExpression(variable_dict, declarator.Initializer.Value, depth);
						}
						var symbol = (ILocalSymbol)_TypeDatabase.FindSymbol(declarator);
						variable_dict.Add(declarator.Identifier.Text, new Variable(depth - 1, symbol));
					}
				}
				break;
			}

			foreach(var child in childlen) {
				action(variable_dict, child);
				VisitStatement(variable_dict, child, action, depth + 1);
			}

			var keys = variable_dict.Where(p => p.Value.Depth >= depth).Select(p => p.Key).ToArray();
			foreach(var key in keys) {
				variable_dict.Remove(key);
			}
		}

		void VisitExpression(Dictionary<string, Variable> variable_dict, ExpressionSyntax expression, int depth)
		{
			switch(expression.Kind()) {
			case SyntaxKind.LogicalAndExpression:
			case SyntaxKind.LogicalOrExpression: {
					var binary = (BinaryExpressionSyntax)expression;
					VisitExpression(variable_dict, binary.Left, depth);
					VisitExpression(variable_dict, binary.Right, depth);
				}
				break;

			case SyntaxKind.LogicalNotExpression: {
					var prefix_unary = (PrefixUnaryExpressionSyntax)expression;
					VisitExpression(variable_dict, prefix_unary.Operand, depth);
				}
				break;

			case SyntaxKind.SimpleMemberAccessExpression: {
					var member_access = (MemberAccessExpressionSyntax)expression;
					//if(variable_dict.Values.FirstOrDefault)
					switch(member_access.Expression.Kind()) {
					case SyntaxKind.IdentifierName: {
							var identifier = (IdentifierNameSyntax)member_access.Expression;
							if(variable_dict.TryGetValue(identifier.Identifier.Text, out var variable)) {
								variable.HasReferenced = true;
							}
						}
						break;

					default:
						VisitExpression(variable_dict, member_access.Expression, depth);
						break;
					}
				}
				break;

			case SyntaxKind.InvocationExpression: {
					var invocation = (InvocationExpressionSyntax)expression;
					VisitExpression(variable_dict, invocation.Expression, depth);
					foreach(var arg in invocation.ArgumentList.Arguments) {
						VisitExpression(variable_dict, arg.Expression, depth);
					}
				}
				break;

			case SyntaxKind.IsPatternExpression: {
					var is_pattern = (IsPatternExpressionSyntax)expression;
					if(is_pattern.Pattern.Kind() != SyntaxKind.DeclarationPattern) {
						break;
					}
					var dec_pattern = (DeclarationPatternSyntax)is_pattern.Pattern;
					var symbol = (ILocalSymbol)_TypeDatabase.FindSymbol((SingleVariableDesignationSyntax)dec_pattern.Designation);
					variable_dict.Add(symbol.Name, new Variable(depth, symbol));
				}
				break;
			}
		}

		public override string ToString() => _Symbol.GetFqn();

		public sealed class Variable
		{
			public ISymbol Symbol { get; }

			public ITypeSymbol TypeSymbol { get; }

			public int Depth { get; }

			public bool HasReferenced { get; set; }

			public Variable(int depth, ISymbol symbol)
			{
				Depth = depth;
				Symbol = symbol;
				TypeSymbol = symbol?.ContainingType;
			}

			public override string ToString() => $"{Depth}, {Symbol.ToString()}";
		}
	}
}