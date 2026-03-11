using Moongate.Server.Data.Internal.Scripting;

namespace Moongate.Server.Interfaces.Services.Scripting;

public interface IBookTemplateService
{
    bool TryLoad(string bookId, IReadOnlyDictionary<string, object?>? model, out BookTemplateContent? book);
}
