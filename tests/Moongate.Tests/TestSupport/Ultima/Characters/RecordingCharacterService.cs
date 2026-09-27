using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Characters;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Tests.TestSupport.Ultima.Characters;

/// <summary>
///     Records what handlers ask of the character service and answers with configured results.
/// </summary>
public sealed class RecordingCharacterService : ICharacterService
{
    public Serial? CreatedFor { get; private set; }
    public CharacterCreationRequest? Request { get; private set; }
    public int CreateCalls { get; private set; }

    /// <summary>
    ///     Gets or sets what <see cref="CreateAsync" /> returns; by default a created character named after the request.
    /// </summary>
    public CharacterCreationResult? Result { get; set; }

    /// <summary>
    ///     Gets or sets what <see cref="GetCharactersAsync" /> returns.
    /// </summary>
    public List<MobileEntity> Characters { get; set; } = [];

    /// <summary>
    ///     Gets or sets an exception both members throw instead of answering, as a failing database would.
    /// </summary>
    public Exception? Failure { get; set; }

    public Task<CharacterCreationResult> CreateAsync(
        Serial accountId,
        CharacterCreationRequest request,
        CancellationToken cancellationToken = default
    )
    {
        CreateCalls++;
        CreatedFor = accountId;
        Request = request;

        if (Failure is not null)
        {
            return Task.FromException<CharacterCreationResult>(Failure);
        }

        return Task.FromResult(
            Result ?? CharacterCreationResult.Created(new MobileEntity { Id = new(1), Name = request.Name }, [])
        );
    }

    public Task<IReadOnlyList<MobileEntity>> GetCharactersAsync(Serial accountId, CancellationToken cancellationToken = default)
    {
        if (Failure is not null)
        {
            return Task.FromException<IReadOnlyList<MobileEntity>>(Failure);
        }

        return Task.FromResult<IReadOnlyList<MobileEntity>>(Characters);
    }
}
