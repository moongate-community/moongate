using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Moongate.Server.Ultima.Services.Books;
using Moongate.Server.Ultima.Services.Text;
using Moongate.UoxItemConverter.Data.Internal.Books;

namespace Moongate.UoxItemConverter.Internal;

/// <summary>
///     Reads literal BookContent definitions as syntax, without compiling or executing emulator code.
/// </summary>
internal static class ModernUoBookSourceReader
{
    public static IReadOnlyList<ImportedBook> Read(string source, string path)
    {
        var tree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview), path);
        var fields = tree.GetRoot().DescendantNodes().OfType<FieldDeclarationSyntax>()
            .Where(field => IsType(field.Declaration.Type, "BookContent")).ToArray();
        if (fields.Length == 0) return [];

        var syntaxError = tree.GetDiagnostics().FirstOrDefault(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        if (syntaxError is not null) throw new InvalidDataException(syntaxError.ToString());

        var books = new List<ImportedBook>();
        foreach (var field in fields)
        {
            var owner = field.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
            var name = owner?.Identifier.ValueText ?? "unknown class";
            try
            {
                if (owner is null || !field.Modifiers.Any(SyntaxKind.StaticKeyword) ||
                    !field.Modifiers.Any(SyntaxKind.ReadOnlyKeyword))
                    throw new InvalidDataException("BookContent must be a static readonly class field.");

                foreach (var variable in field.Declaration.Variables)
                {
                    if (variable.Initializer is null) throw new InvalidDataException("BookContent has no initializer.");
                    var arguments = Arguments(variable.Initializer.Value, "BookContent");
                    if (arguments.Count < 3) throw new InvalidDataException("BookContent needs a title, author and pages.");
                    var title = Literal(arguments[0].Expression);
                    var author = Literal(arguments[1].Expression);
                    var pages = arguments.Skip(2).Select(argument => string.Join('\n',
                        Arguments(argument.Expression, "BookPageInfo").Select(line => Literal(line.Expression)))).ToArray();
                    var content = string.Join("\n\n", pages);
                    var id = "modernuo_" + JsonNamingPolicy.SnakeCaseLower.ConvertName(name);
                    if (!TextTemplateTokens.IsValidName(id) || string.IsNullOrWhiteSpace(title) ||
                        string.IsNullOrWhiteSpace(content) ||
                        !BookTextValidation.IsValidText(title, BookTextValidation.HeaderLimit) ||
                        !BookTextValidation.IsValidText(author, BookTextValidation.HeaderLimit) ||
                        !BookTextValidation.IsValidText(content, BookTextValidation.ContentLimit))
                        throw new InvalidDataException("Book id or text is invalid or exceeds the document limits.");

                    books.Add(new() { Id = id, Title = title, Author = author, Content = content, PageCount = pages.Length });
                }
            }
            catch (InvalidDataException exception)
            {
                throw new InvalidDataException($"{path}: {name}: {exception.Message}", exception);
            }
        }
        return books;
    }

    private static SeparatedSyntaxList<ArgumentSyntax> Arguments(ExpressionSyntax expression, string type)
    {
        var list = expression switch
        {
            ImplicitObjectCreationExpressionSyntax { Initializer: null } creation => creation.ArgumentList,
            ObjectCreationExpressionSyntax { Initializer: null } creation when IsType(creation.Type, type) => creation.ArgumentList,
            _ => null
        };
        if (list is null || list.Arguments.Any(argument => argument.NameColon is not null ||
                                                         !argument.RefKindKeyword.IsKind(SyntaxKind.None)))
            throw new InvalidDataException($"Expected literal {type} constructor arguments.");
        return list.Arguments;
    }

    private static bool IsType(TypeSyntax type, string name)
    {
        return type switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText == name,
            QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText == name,
            AliasQualifiedNameSyntax alias => alias.Name.Identifier.ValueText == name,
            _ => false
        };
    }

    private static string Literal(ExpressionSyntax expression)
    {
        return expression switch
        {
            LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression) => literal.Token.ValueText,
            ParenthesizedExpressionSyntax parentheses => Literal(parentheses.Expression),
            BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.AddExpression) => Literal(binary.Left) + Literal(binary.Right),
            _ => throw new InvalidDataException("Only literal strings are supported; runtime expressions are not executed.")
        };
    }
}
