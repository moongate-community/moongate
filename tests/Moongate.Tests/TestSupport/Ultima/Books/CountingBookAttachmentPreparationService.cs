using Moongate.Server.Ultima.Data.Templates.Books;
using Moongate.Server.Ultima.Interfaces.Books;

namespace Moongate.Tests.TestSupport.Ultima.Books;

internal sealed class CountingBookAttachmentPreparationService : IBookAttachmentPreparationService
{
    private readonly IBookAttachmentPreparationService _inner;
    public int Calls { get; private set; }
    public int? FailOnCall { get; set; }

    public CountingBookAttachmentPreparationService(IBookAttachmentPreparationService inner)
    {
        _inner = inner;
    }

    public string? Prepare(BookTemplateSource source)
    {
        Calls++;
        if (Calls == FailOnCall)
        {
            throw new InvalidDataException("Attachment preparation failed after an earlier saved letter.");
        }
        return _inner.Prepare(source);
    }
}
