namespace Moongate.Server.Attributes;

/// <summary>
/// Marks a type for source-generated Lua user-data registration during bootstrap.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class RegisterLuaUserDataAttribute : Attribute { }
