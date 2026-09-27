namespace Moongate.UoxItemConverter.Internal;

/// <summary>
///     One
///     <c>
///         [header] { ... }
///     </c>
///     block read from a UOX3
///     <c>
///         .dfn
///     </c>
///     file: <see cref="Fields" />
///     for an item block's flat
///     <c>
///         key=value
///     </c>
///     lines, <see cref="Entries" /> for every line verbatim,
///     which is what a
///     <c>
///         [LOOTLIST ...]
///     </c>
///     block's bare, unkeyed entry lines need instead. <see cref="Comments" /> holds the
///     <c>
///         //
///     </c>
///     comment of each field's line and <see cref="EntryComments" /> the comment of every line, in step with
///     <see cref="Entries" />: UOX3 writes
///     <c>
///         NAME=#//an orc
///     </c>
///     and
///     <c>
///         3009//a daemon
///     </c>
///     , where the comment is the only readable text. <see cref="Label" /> is the text after the opening brace,
///     such as
///     <c>
///         { Human Male
///     </c>
///     .
/// </summary>
internal sealed record DfnBlock(
    string Header,
    Dictionary<string, string> Fields,
    List<string> Entries,
    Dictionary<string, string> Comments,
    string? Label,
    List<string?> EntryComments
);
