namespace Moongate.Server.PacketHandlers.Generators.Data.Internal;

internal sealed class ConsoleCommandRegistrationModel : IEquatable<ConsoleCommandRegistrationModel>
{
    public ConsoleCommandRegistrationModel(
        string executorTypeName,
        string commandName,
        string description,
        int sourceValue,
        int minimumAccountTypeValue
    )
    {
        ExecutorTypeName = executorTypeName;
        CommandName = commandName;
        Description = description;
        SourceValue = sourceValue;
        MinimumAccountTypeValue = minimumAccountTypeValue;
    }

    public string ExecutorTypeName { get; }

    public string CommandName { get; }

    public string Description { get; }

    public int SourceValue { get; }

    public int MinimumAccountTypeValue { get; }

    public bool Equals(ConsoleCommandRegistrationModel? other)
    {
        if (other is null)
        {
            return false;
        }

        return ExecutorTypeName == other.ExecutorTypeName &&
               CommandName == other.CommandName &&
               Description == other.Description &&
               SourceValue == other.SourceValue &&
               MinimumAccountTypeValue == other.MinimumAccountTypeValue;
    }

    public override bool Equals(object? obj)
        => obj is ConsoleCommandRegistrationModel other && Equals(other);

    public override int GetHashCode()
    {
        var hash = ExecutorTypeName.GetHashCode();
        hash = (hash * 397) ^ CommandName.GetHashCode();
        hash = (hash * 397) ^ Description.GetHashCode();
        hash = (hash * 397) ^ SourceValue;
        hash = (hash * 397) ^ MinimumAccountTypeValue;

        return hash;
    }
}
