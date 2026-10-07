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

    public Task<IReadOnlyList<MobileEntity>> GetCharactersAsync(
        Serial accountId, CancellationToken cancellationToken = default
    )
    {
        if (Failure is not null)
        {
            return Task.FromException<IReadOnlyList<MobileEntity>>(Failure);
        }

        return Task.FromResult<IReadOnlyList<MobileEntity>>(Characters);
    }

    /// <summary>
    ///     Gets the list index of the last deletion request.
    /// </summary>
    public int? DeletionIndex { get; private set; }

    /// <summary>
    ///     Gets or sets what <see cref="RequestDeletionAsync" /> returns.
    /// </summary>
    public CharacterDeletionResult? DeletionResult { get; set; }

    public Task<IReadOnlyList<MobileEntity>> GetPendingDeletionsAsync(
        Serial? accountId,
        CancellationToken cancellationToken = default
    )
    {
        return Task.FromResult<IReadOnlyList<MobileEntity>>(
            Characters.Where(c => c.DeletionRequestedAt is not null).ToList()
        );
    }

    public Task<CharacterDeletionResult> RequestDeletionAsync(
        Serial accountId,
        int listIndex,
        CancellationToken cancellationToken = default
    )
    {
        DeletionIndex = listIndex;

        if (Failure is not null)
        {
            return Task.FromException<CharacterDeletionResult>(Failure);
        }

        return Task.FromResult(
            DeletionResult ??
            CharacterDeletionResult.Deleted(
                new MobileEntity { Id = new(1), Name = "deleted" },
                [null, null, null, null, null]
            )
        );
    }

    public Task<MobileEntity?> RestoreAsync(Serial characterId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Characters.FirstOrDefault(c => c.Id == characterId && c.DeletionRequestedAt is not null));
    }

    /// <summary>
    ///     Gets or sets what <see cref="GetForPlayAsync" /> returns.
    /// </summary>
    public CharacterForPlay? ForPlay { get; set; }

    /// <summary>
    ///     Gets the list index of the last play request.
    /// </summary>
    public int? PlayIndex { get; private set; }

    public Task<CharacterForPlay?> GetForPlayAsync(
        Serial accountId, int listIndex, CancellationToken cancellationToken = default
    )
    {
        PlayIndex = listIndex;

        return Failure is not null ? Task.FromException<CharacterForPlay?>(Failure) : Task.FromResult(ForPlay);
    }
}
