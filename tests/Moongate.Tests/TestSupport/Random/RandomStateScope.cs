using Moongate.Core.Random;

namespace Moongate.Tests.TestSupport.Random;

public sealed class RandomStateScope : IDisposable
{
    private readonly ulong[] _states;

    public RandomStateScope(ulong seed = 12345)
    {
        var generator = BuiltInRng.Generator;
        _states = Enumerable.Range(0, generator.StateCount).Select(generator.SelectState).ToArray();
        generator.Seed(seed);
    }

    public void Dispose()
    {
        for (var i = 0; i < _states.Length; i++)
        {
            BuiltInRng.Generator.SetSelectedState(i, _states[i]);
        }
    }
}
