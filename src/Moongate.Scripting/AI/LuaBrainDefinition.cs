using Moongate.Server.Abstractions.Data.AI;
using MoonSharp.Interpreter;

namespace Moongate.Scripting.AI;

internal sealed record LuaBrainDefinition(
    BrainDescriptor Descriptor,
    Table Strategy
);
