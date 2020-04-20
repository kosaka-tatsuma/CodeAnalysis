# CodeAnalysis

C#の静的解析を検証＆開発するプロジェクト

## ITypeSymbolの構造について

### 格納元シンボルへの参照

ISymbol.ContainingXXXを使用することで格納元アセンブリ、モジュール、名前空間、型、シンボルを取得できます。  

### メソッド内部へのアクセスはできる

ISymbol.DeclaringSyntaxReferencesを使用します。  
これは各定義のSyntaxNodeへのアクセスを可能にします。  

## SyntaxNodeの構造について

### 変数定義

変数定義には、以下の種類があります。

+ FieldDeclarationSyntax
+ EventFieldDeclarationSyntax
+ PropertyDeclarationSyntax
+ AccessorDeclarationSyntax

また、これらの定義は共通してVariableDeclarationSyntaxを内包しています。  

