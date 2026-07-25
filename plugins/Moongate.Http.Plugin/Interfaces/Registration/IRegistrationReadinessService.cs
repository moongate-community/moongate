using Moongate.Http.Plugin.Data.Registration;

namespace Moongate.Http.Plugin.Interfaces.Registration;

/// <summary>Evaluates whether local configuration makes public account registration available.</summary>
public interface IRegistrationReadinessService
{
    /// <summary>Evaluates the readiness prerequisites for the supplied public website.</summary>
    RegistrationReadiness Evaluate(string? website);
}
