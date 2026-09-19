namespace Moongate.Api.Exceptions;

/// <summary>The call was rejected locally before sending because bounded capacity is exhausted.</summary>
public sealed class ApiBusyException : InvalidOperationException
{
    public ApiBusyException() : base("The API connection has no available call capacity.") { }
}
