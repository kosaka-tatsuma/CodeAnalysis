# CodeAnalysis

C#の静的解析を検証＆開発するプロジェクト

## SyntaxNodeの構造について

### 変数定義

変数定義には、以下の種類があります。

+ FieldDeclarationSyntax
+ EventFieldDeclarationSyntax
+ PropertyDeclarationSyntax
+ AccessorDeclarationSyntax

また、これらの定義は共通してVariableDeclarationSyntaxを内包しています。
