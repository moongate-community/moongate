namespace Moongate.Tests.TestSupport.Environment;

public sealed class EnvironmentVariableScope : IDisposable
{
    private readonly string _name;
    private readonly string? _originalValue;

    public EnvironmentVariableScope(string name, string? value)
    {
        _name = name;
        _originalValue = System.Environment.GetEnvironmentVariable(name);
        System.Environment.SetEnvironmentVariable(name, value);
    }

    public void Dispose()
        => System.Environment.SetEnvironmentVariable(_name, _originalValue);
}
