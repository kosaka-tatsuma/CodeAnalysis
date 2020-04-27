using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeAnalysis
{
	using System.Diagnostics;
	using Microsoft.CodeAnalysis;
	using Microsoft.CodeAnalysis.CSharp;
	using Microsoft.CodeAnalysis.CSharp.Syntax;

	public static class Util
	{
		#region Diagnostics
		class AssertException : Exception
		{
			public AssertException(string msg) : base(msg) { }
		}

		[Conditional("DEBUG"), Conditional("RELEASE")]
		public static void Assert(bool condition, string msg)
		{
			if(condition == false) {
				throw new AssertException(msg);
			}
		}

		static void WriteLine(ConsoleColor color, string msg)
		{
			var old_color = Console.ForegroundColor;
			Console.ForegroundColor = color;
			Console.WriteLine(msg);
			Console.ForegroundColor = old_color;
		} 

		[Conditional("DEBUG"), Conditional("RELEASE")]
		public static void InfoLine(string msg) => Console.WriteLine(msg);

		[Conditional("DEBUG"), Conditional("RELEASE")]
		public static void WarningLine(string msg) => WriteLine(ConsoleColor.Yellow, msg);

		[Conditional("DEBUG"), Conditional("RELEASE")]
		public static void ErrorLine(string msg) => WriteLine(ConsoleColor.Red, msg);

		[Conditional("DEBUG"), Conditional("RELEASE")]
		public static void ErrorLineIf(bool condition, string msg)
		{
			if(condition) {
				ErrorLine(msg);
			}
		}
		#endregion
	}

	public static class SymbolExtension
	{
		public static string GetFqn(this ISymbol self)
		{
			var ret = self.Name;

			void core(Func<ISymbol, ISymbol> func)
			{
				var symbol = func(self);
				while(symbol != null) {
					ret = symbol.Name + "." + ret;
					symbol = func(symbol);
				}
			}

			core(symbol => symbol.ContainingType);
			core(symbol => symbol.ContainingNamespace.IsGlobalNamespace? null : symbol.ContainingNamespace);
			return ret;
		}
	}

	public static class SyntaxExtension
	{
		public static string GetFqn(this TypeSyntax self)
		{
			switch(self.Kind()) {
			case SyntaxKind.PredefinedType: {
					var predefined = (PredefinedTypeSyntax)self;
					return predefined.Keyword.Text;
				}
			case SyntaxKind.IdentifierName: {
					var id = (IdentifierNameSyntax)self;
					return id.Identifier.Text;
				}
			case SyntaxKind.QualifiedName: {
					var qual = (QualifiedNameSyntax)self;
					return qual.Left.GetFqn() + qual.DotToken.Text + qual.Right.Identifier.Text;
				}
			case SyntaxKind.ArrayType: {
					var array_type = (ArrayTypeSyntax)self;
					var tmp = array_type.ElementType.GetFqn();
					foreach(var rank_specifier in array_type.RankSpecifiers) {
						tmp += (rank_specifier.OpenBracketToken.Text + string.Concat(Enumerable.Range(0, rank_specifier.Sizes.Count - 1).Select(i => ", ")) + rank_specifier.CloseBracketToken.Text);
					}
					return tmp;
				}
			case SyntaxKind.GenericName: {
					var generic = (GenericNameSyntax)self;
					var type_arg_list = generic.TypeArgumentList;
					return generic.Identifier.Text + type_arg_list.LessThanToken.Text + string.Join(", ", type_arg_list.Arguments.Select(e => e.GetFqn())) + type_arg_list.GreaterThanToken.Text;
				}
			case SyntaxKind.TupleType: {
					var tuple_type = (TupleTypeSyntax)self;
					return tuple_type.OpenParenToken.Text + string.Join(", ", tuple_type.Elements.Select(e => e.Type.GetFqn() + " " + e.Identifier.Text)) + tuple_type.CloseParenToken.Text;
				}
			case SyntaxKind.NullableType: {
					var nullable = (NullableTypeSyntax)self;
					return nullable.ElementType.GetFqn() + nullable.QuestionToken.Text;
				}
			}

			throw new ArgumentException($"Kind {self.Kind()} is not support.");
		}
	}
}
