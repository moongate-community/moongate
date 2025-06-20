using Moongate.Core.Server.Types;
using NanoidDotNet;

namespace Moongate.UO.Data.Persistence.Entities;

public class UOAccountEntity
{
    public string Id { get; set; }

    public string Username { get; set; }

    public string HashedPassword { get; set; }

    public AccountLevelType AccountLevel { get; set; }

    public DateTime Created { get; set; }

    public DateTime LastLogin { get; set; }

    public bool IsActive { get; set; }

    public List<UOAccountCharacterEntity> Characters { get; set; } = new();


    public UOAccountCharacterEntity GetCharacter(int slot)
    {
        return Characters.FirstOrDefault(c => c.Slot == slot);
    }

    public UOAccountCharacterEntity? GetCharacter(string name)
    {
        return Characters.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    public void RemoveCharacter(UOAccountCharacterEntity character)
    {
        Characters.Remove(character);
        // Reassign slots after removal
        for (int i = 0; i < Characters.Count; i++)
        {
            Characters[i].Slot = i;
        }
    }

    public void AddCharacter(UOMobileEntity mobileEntity)
    {
        AddCharacter(
            new UOAccountCharacterEntity()
            {
                Slot = Characters.Count,
                MobileId = mobileEntity.Id,
            }
        );
    }

    public void AddCharacter(UOAccountCharacterEntity character)
    {
        Characters.Add(character);
    }

    public UOAccountEntity()
    {
        Id = Nanoid.Generate();
        Created = DateTime.Now;
    }
}
