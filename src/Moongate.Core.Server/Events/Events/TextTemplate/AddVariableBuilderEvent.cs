
namespace Moongate.Core.Server.Events.Events.TextTemplate;

public record AddVariableBuilderEvent(string VariableName, Func<object> Builder);
