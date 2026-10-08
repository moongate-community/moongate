using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Moongate.UoxItemConverter.Data.Internal.Vendors;

namespace Moongate.UoxItemConverter.Internal;

/// <summary>
///     Reads the shops of ModernUO as syntax, without compiling or running anything: the lines an <c>SBInfo</c> sells
///     and the <c>SBInfo</c> classes a vendor adds. What sits under a condition is left out and counted in the report.
/// </summary>
internal static class ModernUoVendorSourceReader
{
    private const string SbInfoBase = "SBInfo";
    private const string BuyInfoClass = "InternalBuyInfo";
    private const string InitMethod = "InitSBInfo";
    private const string AnimalLine = "AnimalBuyInfo";
    private const string BeverageLine = "BeverageBuyInfo";
    private const string GenericLine = "GenericBuyInfo";

    /// <summary>
    ///     Reads the <c>SBInfo</c> classes of a file.
    /// </summary>
    public static IReadOnlyList<ImportedSbInfo> ReadSbInfos(string source, string path, ConversionReport report)
    {
        var tree = Parse(source, path);
        var infos = new List<ImportedSbInfo>();

        foreach (var owner in tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>())
        {
            if (owner.BaseList?.Types.Any(type => type.Type.ToString() == SbInfoBase) != true)
            {
                continue;
            }

            var lines = new List<ImportedBuyLine>();
            var buyInfo = owner.Members.OfType<ClassDeclarationSyntax>()
                .FirstOrDefault(member => member.Identifier.ValueText == BuyInfoClass);

            foreach (var constructor in buyInfo?.Members.OfType<ConstructorDeclarationSyntax>() ?? [])
            {
                foreach (var creation in constructor.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
                {
                    ReadLine(creation, constructor, lines, report);
                }
            }

            infos.Add(new() { Name = owner.Identifier.ValueText, Lines = lines });
        }

        return infos;
    }

    /// <summary>
    ///     Reads the vendor classes of a file: those with an <c>InitSBInfo</c> method.
    /// </summary>
    public static IReadOnlyList<ImportedVendor> ReadVendors(string source, string path, ConversionReport report)
    {
        var tree = Parse(source, path);
        var vendors = new List<ImportedVendor>();

        foreach (var owner in tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>())
        {
            var method = owner.Members.OfType<MethodDeclarationSyntax>()
                .FirstOrDefault(member => member.Identifier.ValueText == InitMethod);

            if (method is null)
            {
                continue;
            }

            var names = new List<string>();

            foreach (var creation in method.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
            {
                var name = creation.Type.ToString();

                if (!name.StartsWith("SB", StringComparison.Ordinal))
                {
                    continue;
                }

                if (IsConditional(creation, method))
                {
                    report.Count($"conditional SBInfo {name}");

                    continue;
                }

                names.Add(name);
            }

            vendors.Add(new() { Name = owner.Identifier.ValueText, SbInfos = names });
        }

        return vendors;
    }

    private static SyntaxTree Parse(string source, string path)
    {
        var tree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview), path);

        if (tree.GetDiagnostics().FirstOrDefault(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error) is { } error)
        {
            throw new InvalidDataException(error.ToString());
        }

        return tree;
    }

    // Under an if, an else or a loop: it depends on the era or on the vendor, so it is not part of the shop. A switch
    // picks a set at random for each vendor, so the shop takes all the sets.
    private static bool IsConditional(SyntaxNode node, SyntaxNode root)
    {
        return node.Ancestors()
            .TakeWhile(ancestor => ancestor != root)
            .Any(ancestor => ancestor is IfStatementSyntax
                or ElseClauseSyntax or ForStatementSyntax or ForEachStatementSyntax or WhileStatementSyntax
            );
    }

    private static void ReadLine(
        ObjectCreationExpressionSyntax creation,
        SyntaxNode constructor,
        List<ImportedBuyLine> lines,
        ConversionReport report
    )
    {
        var kind = creation.Type.ToString();

        if (kind is not (GenericLine or BeverageLine or AnimalLine))
        {
            return;
        }

        if (kind == AnimalLine)
        {
            report.Count("animal line (pets are not sold yet)");

            return;
        }

        if (IsConditional(creation, constructor))
        {
            report.Count("conditional line");

            return;
        }

        var arguments = creation.ArgumentList?.Arguments.Select(argument => argument.Expression).ToList() ?? [];
        var typeAt = arguments.FindIndex(argument => argument is TypeOfExpressionSyntax);

        if (typeAt < 0 || arguments[typeAt] is not TypeOfExpressionSyntax typeOf)
        {
            report.Count("line without a type");

            return;
        }

        var name = typeAt > 0 && arguments[typeAt - 1] is LiteralExpressionSyntax { Token.Value: string text } ? text : "";
        // A beverage names its content between the type and the price.
        var numbersAt = typeAt + 1 + (kind == BeverageLine ? 1 : 0);
        var numbers = arguments.Skip(numbersAt).Take(4).Select(Number).ToList();

        if (numbers.Count < 4 || numbers[0] is not { } price || numbers[1] is not { } amount ||
            numbers[2] is not { } graphic)
        {
            report.Count("line with a number that is no literal");

            return;
        }

        if (numbers[3] is null)
        {
            report.Count("hue that is no literal (taken as 0)");
        }

        lines.Add(
            new()
            {
                TypeName = typeOf.Type.ToString(),
                Price = price, Amount = amount, Graphic = graphic, Hue = numbers[3] ?? 0, Name = name
            }
        );
    }

    private static int? Number(ExpressionSyntax expression)
    {
        return expression is LiteralExpressionSyntax { Token.Value: int value } ? value : null;
    }
}
