using Moongate.Api.Attributes;
using Moongate.Api.Registry;
using Moongate.Api.Tests.TestSupport.Contracts;
namespace Moongate.Api.Tests.Registry;
public sealed class ApiRegistryTests
{
    [Fact]
    public void Freeze_ResolvesHandlerOnceAndPreventsFurtherRegistration()
    {
        var registry = new ApiRegistry();
        var resolutions = 0;
        registry.RegisterContract<IncrementRequest, IncrementResponse>();
        registry.RegisterHandler(() => { resolutions++; return new IncrementHandler(); });
        Assert.Equal(0, resolutions);
        Assert.Equal(1, registry.ContractCount);
        Assert.Equal(1, registry.HandlerCount);
        registry.Freeze();
        registry.Freeze();
        Assert.Equal(1, resolutions);
        Assert.Throws<InvalidOperationException>(() => registry.RegisterHandler(() => new IncrementHandler()));
        Assert.Equal(1, resolutions);
    }

    [Fact]
    public void RegisterContract_DuplicateWireId_LeavesExistingRegistrationIntact()
    {
        var registry = new ApiRegistry();
        registry.RegisterContract<IncrementRequest, IncrementResponse>();
        Assert.Throws<InvalidOperationException>(() => registry.RegisterContract<AlternativeRequest, IncrementResponse>());
        Assert.Equal(1, registry.ContractCount);
    }

    [Fact]
    public void RegisterHandler_AfterFreeze_DoesNotInvokeFactory()
    {
        var registry = new ApiRegistry();
        registry.Freeze();
        var invoked = false;
        Assert.Throws<InvalidOperationException>(() => registry.RegisterHandler(() => { invoked = true; return new IncrementHandler(); }));
        Assert.False(invoked);
        Assert.Equal(0, registry.HandlerCount);
    }

    [Fact]
    public void ValidateHandler_InvalidOrAmbiguousType_DoesNotMutateRegistry()
    {
        var registry = new ApiRegistry();
        Assert.Throws<InvalidOperationException>(() => registry.ValidateHandler(typeof(string)));
        Assert.Throws<InvalidOperationException>(() => registry.ValidateHandler(typeof(AmbiguousHandler)));
        Assert.Throws<InvalidOperationException>(() => registry.RegisterContract<UnannotatedRequest, IncrementResponse>());
        Assert.Throws<ArgumentOutOfRangeException>(() => new ApiOperationAttribute(0));
        Assert.Equal(0, registry.ContractCount);
    }

    [Fact]
    public void Freeze_FactoryFailure_RemainsFailedOnRetry()
    {
        var registry = new ApiRegistry();
        registry.RegisterHandler<IncrementHandler>(() => null!);
        Assert.Throws<InvalidOperationException>(() => registry.Freeze());
        Assert.Throws<InvalidOperationException>(() => registry.Freeze());
    }
}
