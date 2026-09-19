using System.Text.RegularExpressions;

namespace Moongate.Tests.Conventions;

/// <summary>
/// Guards CODE_CONVENTION 4.2: an IDE rearrange pass sorts members by kind and accessibility, which lifts
/// Dispose above the methods it tears down. The rule is not expressible in .editorconfig, so this test is
/// what actually fails when a formatter moves it.
/// </summary>
public sealed class DisposeMemberOrderTests
{
    private static readonly Regex MemberDeclaration = new(
        @"^\s*(?:\[.*\]\s*)?(?:public|private|protected|internal)\s",
        RegexOptions.Compiled
    );

    private static readonly Regex TypeDeclaration = new(
        @"\b(?:class|struct|record|interface|enum)\s+\w",
        RegexOptions.Compiled
    );

    private static readonly Regex DisposeMember = new(
        @"\b(?:Dispose|DisposeAsync)\s*\(",
        RegexOptions.Compiled
    );

    [Fact]
    public void Dispose_IsDeclaredAfterEveryOtherMemberOfItsType()
    {
        var root = RepositoryRoot();
        var offenders = new List<string>();

        foreach (var file in EnumerateProductionSources(root))
        {
            var trailing = MembersAfterLastDispose(File.ReadAllLines(file));

            if (trailing.Count > 0)
            {
                offenders.Add($"{Path.GetRelativePath(root, file)} declares {string.Join(", ", trailing)} after Dispose");
            }
        }

        Assert.Empty(offenders);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Moongate.slnx")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("The repository root was not found above " + AppContext.BaseDirectory);
        }

        return directory.FullName;
    }

    private static IEnumerable<string> EnumerateProductionSources(string root)
    {
        return Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
                        .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                                       && !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));
    }

    /// <summary>
    /// Names the top-level members declared after the last Dispose member of the file. Nesting depth keeps the
    /// scan on the outer type, so members of a nested type never count as trailing.
    /// </summary>
    private static List<string> MembersAfterLastDispose(IReadOnlyList<string> lines)
    {
        var trailing = new List<string>();
        var seenDispose = false;
        var depth = 0;

        foreach (var line in lines)
        {
            if (depth <= 1 && MemberDeclaration.IsMatch(line) && !TypeDeclaration.IsMatch(line))
            {
                if (DisposeMember.IsMatch(line))
                {
                    seenDispose = true;
                    trailing.Clear();
                }
                else if (seenDispose)
                {
                    trailing.Add(line.Trim());
                }
            }

            depth += line.Count(character => character == '{') - line.Count(character => character == '}');
        }

        return trailing;
    }
}
