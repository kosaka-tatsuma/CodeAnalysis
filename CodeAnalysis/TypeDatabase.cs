using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

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

		public TypeDatabase()
		{
			_TypeSymbolList = new ReadOnlyCollection<TypeSymbolContainer>(_TypeSymbolListInternal);
		}

		public void Build(ProjectAnalyzer analyzer)
		{
			using(var ws = analyzer.GetWorkspace()) {
				foreach(var proj in ws.CurrentSolution.Projects) {
					//--- ログの蓄積フォルダの生成 ---//
					var dirinfo = System.IO.Directory.CreateDirectory(FootprintFolderName + "\\" + proj.Name);

					var comp = proj.GetCompilationAsync().Result;
					foreach(var cls_symbol in comp.GetSymbolsWithName(_ => true, SymbolFilter.Type).OfType<ITypeSymbol>()) {
						var container = new TypeSymbolContainer(cls_symbol);
						_TypeSymbolListInternal.Add(container);
					}

					foreach(var doc in proj.Documents) {
						using(var writer = new System.IO.StreamWriter(dirinfo.FullName +  $"\\footprint_{doc.Name}.log")) {
							var walker = new Walker() { Writer = writer };
							writer.WriteLine(doc.FilePath);
							if(System.IO.Path.GetExtension(doc.FilePath) == ".cs" && doc.TryGetSyntaxRoot(out var root)) {
								walker.Visit(root);
							}
							writer.WriteLine("--- End");

							//foreach(var method_node in walker._MethodList) {
							//	method_node.Parent
							//}
						}
					}
				}
			}
		}
		
		bool CheckSameMethod(IMethodSymbol symbol, BaseMethodDeclarationSyntax node)
		{
			return false;
		}

		class Walker : SyntaxWalker
		{
			public System.IO.TextWriter Writer { get; set; }

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

			string DumpTypeSyntax(TypeSyntax syntax)
			{
				switch(syntax.Kind()) {
				case SyntaxKind.PredefinedType: {
						var predefined = (PredefinedTypeSyntax)syntax;
						return predefined.Keyword.Kind() + $"({predefined.Keyword.Text})";
					}
				case SyntaxKind.IdentifierName: {
						var id = (IdentifierNameSyntax)syntax;
						return id.Identifier.Text;
					}
				case SyntaxKind.QualifiedName: {
						var qual = (QualifiedNameSyntax)syntax;
						return DumpTypeSyntax(qual.Left) + qual.DotToken.Text + qual.Right.Identifier.Text;
					}
				case SyntaxKind.ArrayType: {
						var array_type = (ArrayTypeSyntax)syntax;
						var tmp = DumpTypeSyntax(array_type.ElementType);
						foreach(var rank_specifier in array_type.RankSpecifiers) {
							tmp += (rank_specifier.OpenBracketToken.Text + string.Concat(Enumerable.Range(0, rank_specifier.Sizes.Count - 1).Select(i => ", ")) + rank_specifier.CloseBracketToken.Text);
						}
						return tmp;
					}
				case SyntaxKind.GenericName: {
						var generic = (GenericNameSyntax)syntax;
						var type_arg_list = generic.TypeArgumentList;
						return generic.Identifier.Text + type_arg_list.LessThanToken.Text + string.Join(", ", type_arg_list.Arguments.Select(e => DumpTypeSyntax(e))) + type_arg_list.GreaterThanToken.Text;
					}
				case SyntaxKind.TupleType: {
						var tuple_type = (TupleTypeSyntax)syntax;
						return tuple_type.OpenParenToken.Text + string.Join(", ", tuple_type.Elements.Select(e => DumpTypeSyntax(e.Type) + " " + e.Identifier.Text)) + tuple_type.CloseParenToken.Text;
					}
				case SyntaxKind.NullableType: {
						var nullable = (NullableTypeSyntax)syntax;
						return DumpTypeSyntax(nullable.ElementType) + nullable.QuestionToken.Text;
					}
				}
				
				throw new ArgumentException($"Kind {syntax.Kind()} is not support.");
			}

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
		MethodDeclarationSyntax SyntaxNode { get; set; }

		public MethodSymbolContainer(IMethodSymbol symbol) : base(symbol) { }

		public void Initialize(MethodDeclarationSyntax node)
		{
			SyntaxNode = node;
		}
	}
}