using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Moongate.Server.PacketHandlers.Generators.Data.Internal;

namespace Moongate.Server.PacketHandlers.Generators;

[Generator]
public sealed class PersistenceSnapshotContractGenerator : IIncrementalGenerator
{
    private const string PersistedEntityAttributeName =
        "Moongate.Generators.Annotations.Persistence.MoongatePersistedEntityAttribute";

    private const string PersistedMemberAttributeName =
        "Moongate.Generators.Annotations.Persistence.MoongatePersistedMemberAttribute";

    private const string SerialTypeName = "global::Moongate.UO.Data.Ids.Serial";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) => node is TypeDeclarationSyntax typeDeclaration && typeDeclaration.AttributeLists.Count > 0,
            static (syntaxContext, _) => CreateModel(syntaxContext)
        );

        var models = candidates
                     .Where(static model => model is not null)
                     .Collect();

        context.RegisterSourceOutput(
            models,
            static (productionContext, entityModels) =>
            {
                foreach (var model in entityModels.Where(static current => current is not null)!)
                {
                    productionContext.AddSource(
                        $"{model!.EntityTypeName}Snapshot.g.cs",
                        SourceText.From(BuildSnapshotSource(model), Encoding.UTF8)
                    );
                    productionContext.AddSource(
                        $"{model.EntityTypeName}SnapshotMapper.g.cs",
                        SourceText.From(BuildMapperSource(model), Encoding.UTF8)
                    );
                    productionContext.AddSource(
                        $"{model.EntityTypeName}Persistence.g.cs",
                        SourceText.From(BuildPersistenceMetadataSource(model), Encoding.UTF8)
                    );
                }
            }
        );
    }

    private static PersistenceEntityModel? CreateModel(GeneratorSyntaxContext context)
    {
        if (context.Node is not TypeDeclarationSyntax typeDeclaration)
        {
            return null;
        }

        if (context.SemanticModel.GetDeclaredSymbol(typeDeclaration) is not INamedTypeSymbol typeSymbol || typeSymbol.IsAbstract)
        {
            return null;
        }

        var attribute = typeSymbol.GetAttributes()
                                  .FirstOrDefault(static current =>
                                      current.AttributeClass?.ToDisplayString() == PersistedEntityAttributeName);

        if (attribute is null)
        {
            return null;
        }

        var emitsRootMetadata = false;
        ushort? typeId = null;
        string? typeName = null;
        int? schemaVersion = null;
        string? keyTypeName = null;

        if (attribute.ConstructorArguments.Length == 4 &&
            attribute.ConstructorArguments[0].Value is not null &&
            attribute.ConstructorArguments[1].Value is string persistedTypeName &&
            attribute.ConstructorArguments[2].Value is not null &&
            attribute.ConstructorArguments[3].Value is ITypeSymbol keyTypeSymbol)
        {
            emitsRootMetadata = true;
            typeId = Convert.ToUInt16(attribute.ConstructorArguments[0].Value, System.Globalization.CultureInfo.InvariantCulture);
            typeName = persistedTypeName;
            schemaVersion = Convert.ToInt32(
                attribute.ConstructorArguments[2].Value,
                System.Globalization.CultureInfo.InvariantCulture
            );
            keyTypeName = NormalizeTypeName(keyTypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
        }
        else if (attribute.ConstructorArguments.Length != 0)
        {
            return null;
        }

        var members = typeSymbol
                      .GetMembers()
                      .Where(static member => member is IPropertySymbol or IFieldSymbol)
                      .Where(static member => !member.IsStatic)
                      .Select(static member => CreateMemberModel(member))
                      .Where(static member => member is not null)
                      .OrderBy(static member => member!.Order)
                      .ToArray();

        if (members.Length == 0)
        {
            return null;
        }

        var keyMemberName = emitsRootMetadata
                                ? members.FirstOrDefault(static member => member!.MemberName is "Id" or "MessageId")
                                         ?.MemberName ??
                                  members.FirstOrDefault(member => member!.EntityTypeName == keyTypeName)
                                         ?.MemberName ??
                                  members[0]!.MemberName
                                : null;

        return new(
            typeSymbol.ContainingNamespace.IsGlobalNamespace ? string.Empty : typeSymbol.ContainingNamespace.ToDisplayString(),
            typeSymbol.Name,
            NormalizeTypeName(typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)),
            emitsRootMetadata,
            keyTypeName,
            keyMemberName,
            typeId,
            typeName,
            schemaVersion,
            members!
        );
    }

    private static PersistenceMemberModel? CreateMemberModel(ISymbol memberSymbol)
    {
        var attribute = memberSymbol.GetAttributes()
                                    .FirstOrDefault(static current =>
                                        current.AttributeClass?.ToDisplayString() == PersistedMemberAttributeName);

        if (attribute is null || attribute.ConstructorArguments.Length == 0 || attribute.ConstructorArguments[0].Value is not int order)
        {
            return null;
        }

        var snapshotMemberName =
            attribute.NamedArguments.FirstOrDefault(static pair => pair.Key == "SnapshotName").Value.Value as string ??
            memberSymbol.Name;

        var typeSymbol = memberSymbol switch
        {
            IPropertySymbol property => property.Type,
            IFieldSymbol field       => field.Type,
            _                        => null
        };

        if (typeSymbol is null)
        {
            return null;
        }

        if (IsSerial(typeSymbol))
        {
            return new(
                order,
                memberSymbol.Name,
                snapshotMemberName,
                NormalizeTypeName(typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)),
                "uint",
                PersistenceMemberKind.Serial,
                PersistenceCollectionKind.None,
                null,
                false,
                false
            );
        }

        if (TryGetCollectionElement(typeSymbol, out var collectionKind, out var elementType))
        {
            if (IsSerial(elementType))
            {
                return new(
                    order,
                    memberSymbol.Name,
                    snapshotMemberName,
                    NormalizeTypeName(typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)),
                    "uint[]",
                    PersistenceMemberKind.SerialCollection,
                    collectionKind,
                    null,
                    true,
                    false
                );
            }

            if (TryGetNestedPersistenceTypeName(elementType, out var nestedPersistenceTypeName))
            {
                return new(
                    order,
                    memberSymbol.Name,
                    snapshotMemberName,
                    NormalizeTypeName(typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)),
                    $"{GetSnapshotTypeName(elementType)}[]",
                    PersistenceMemberKind.NestedContractCollection,
                    collectionKind,
                    nestedPersistenceTypeName,
                    true,
                    false
                );
            }

            if (!IsSupportedScalar(elementType))
            {
                return null;
            }

            return new(
                order,
                memberSymbol.Name,
                snapshotMemberName,
                NormalizeTypeName(typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)),
                $"{NormalizeTypeName(elementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))}[]",
                PersistenceMemberKind.ScalarCollection,
                collectionKind,
                null,
                true,
                false
            );
        }

        if (TryGetNestedPersistenceTypeName(typeSymbol, out var nestedTypeName))
        {
            return new(
                order,
                memberSymbol.Name,
                snapshotMemberName,
                NormalizeTypeName(typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)),
                GetSnapshotTypeName(typeSymbol),
                PersistenceMemberKind.NestedContract,
                PersistenceCollectionKind.None,
                nestedTypeName,
                typeSymbol.NullableAnnotation == NullableAnnotation.Annotated,
                false
            );
        }

        if (!IsSupportedScalar(typeSymbol))
        {
            return null;
        }

        return new(
            order,
            memberSymbol.Name,
            snapshotMemberName,
            NormalizeTypeName(typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)),
            NormalizeTypeName(typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)),
            PersistenceMemberKind.Scalar,
            PersistenceCollectionKind.None,
            null,
            typeSymbol.NullableAnnotation == NullableAnnotation.Annotated,
            typeSymbol.SpecialType == SpecialType.System_String
        );
    }

    private static string BuildSnapshotSource(PersistenceEntityModel model)
    {
        var builder = new StringBuilder();

        builder.AppendLine("// <auto-generated/>");
        builder.AppendLine("#pragma warning disable CS1591");
        builder.AppendLine("using MessagePack;");
        builder.AppendLine();

        AppendNamespaceOpen(builder, model.NamespaceName);
        builder.AppendLine("[MessagePackObject(true)]");
        builder.Append("public sealed partial class ");
        builder.Append(model.EntityTypeName);
        builder.AppendLine("Snapshot");
        builder.AppendLine("{");

        foreach (var member in model.Members)
        {
            builder.Append("    public ");
            builder.Append(member.SnapshotTypeName);
            builder.Append(' ');
            builder.Append(member.SnapshotMemberName);
            builder.Append(" { get; set; }");

            if (member.Kind is PersistenceMemberKind.ScalarCollection or PersistenceMemberKind.NestedContractCollection or
                PersistenceMemberKind.SerialCollection)
            {
                builder.Append(" = [];");
            }
            else if (member.IsString && !member.IsNullable)
            {
                builder.Append(" = string.Empty;");
            }
            else if (member.Kind == PersistenceMemberKind.NestedContract && !member.IsNullable)
            {
                builder.Append(" = new();");
            }

            builder.AppendLine();
        }

        builder.AppendLine("}");
        AppendNamespaceClose(builder, model.NamespaceName);

        return builder.ToString();
    }

    private static string BuildMapperSource(PersistenceEntityModel model)
    {
        var builder = new StringBuilder();

        builder.AppendLine("// <auto-generated/>");
        builder.AppendLine("#pragma warning disable CS1591");
        builder.AppendLine("using System.Linq;");
        builder.AppendLine();

        AppendNamespaceOpen(builder, model.NamespaceName);
        builder.Append("internal static partial class ");
        builder.Append(model.EntityTypeName);
        builder.AppendLine("SnapshotMapper");
        builder.AppendLine("{");
        builder.Append("    public static ");
        builder.Append(model.EntityTypeName);
        builder.Append("Snapshot ToSnapshot(");
        builder.Append(model.EntityFullTypeName);
        builder.AppendLine(" entity)");
        builder.AppendLine("        => new()");
        builder.AppendLine("        {");

        for (var i = 0; i < model.Members.Count; i++)
        {
            var member = model.Members[i];
            builder.Append("            ");
            builder.Append(member.SnapshotMemberName);
            builder.Append(" = ");
            builder.Append(BuildToSnapshotExpression(member));
            builder.Append(i == model.Members.Count - 1 ? string.Empty : ",");
            builder.AppendLine();
        }

        builder.AppendLine("        };");
        builder.AppendLine();
        builder.Append("    public static ");
        builder.Append(model.EntityFullTypeName);
        builder.Append(" FromSnapshot(");
        builder.Append(model.EntityTypeName);
        builder.AppendLine("Snapshot snapshot)");
        builder.AppendLine("        => new()");
        builder.AppendLine("        {");

        for (var i = 0; i < model.Members.Count; i++)
        {
            var member = model.Members[i];
            builder.Append("            ");
            builder.Append(member.MemberName);
            builder.Append(" = ");
            builder.Append(BuildFromSnapshotExpression(member));
            builder.Append(i == model.Members.Count - 1 ? string.Empty : ",");
            builder.AppendLine();
        }

        builder.AppendLine("        };");
        builder.AppendLine("}");
        AppendNamespaceClose(builder, model.NamespaceName);

        return builder.ToString();
    }

    private static string BuildPersistenceMetadataSource(PersistenceEntityModel model)
    {
        var builder = new StringBuilder();

        builder.AppendLine("// <auto-generated/>");
        builder.AppendLine("#pragma warning disable CS1591");
        builder.AppendLine();

        AppendNamespaceOpen(builder, model.NamespaceName);
        builder.Append("public static partial class ");
        builder.Append(model.EntityTypeName);
        builder.AppendLine("Persistence");
        builder.AppendLine("{");

        if (model.EmitsRootMetadata)
        {
            builder.Append("    public const ushort TypeId = ");
            builder.Append(model.TypeId);
            builder.AppendLine(";");
            builder.Append("    public const string TypeName = \"");
            builder.Append(model.TypeName!.Replace("\"", "\\\""));
            builder.AppendLine("\";");
            builder.Append("    public const int SchemaVersion = ");
            builder.Append(model.SchemaVersion);
            builder.AppendLine(";");
            builder.Append("    public static global::System.Type KeyType => typeof(");
            builder.Append(model.KeyTypeName);
            builder.AppendLine(");");
            builder.AppendLine();
            builder.Append("    public static ");
            builder.Append(model.KeyTypeName);
            builder.Append(" GetKey(");
            builder.Append(model.EntityFullTypeName);
            builder.Append(" entity) => entity.");
            builder.Append(model.KeyMemberName);
            builder.AppendLine(";");
            builder.AppendLine();
        }

        builder.Append("    public static ");
        builder.Append(model.EntityTypeName);
        builder.Append("Snapshot ToSnapshot(");
        builder.Append(model.EntityFullTypeName);
        builder.Append(" entity) => ");
        builder.Append(model.EntityTypeName);
        builder.AppendLine("SnapshotMapper.ToSnapshot(entity);");
        builder.Append("    public static ");
        builder.Append(model.EntityFullTypeName);
        builder.Append(" FromSnapshot(");
        builder.Append(model.EntityTypeName);
        builder.Append("Snapshot snapshot) => ");
        builder.Append(model.EntityTypeName);
        builder.AppendLine("SnapshotMapper.FromSnapshot(snapshot);");
        builder.AppendLine("}");
        AppendNamespaceClose(builder, model.NamespaceName);

        return builder.ToString();
    }

    private static string BuildToSnapshotExpression(PersistenceMemberModel member)
        => member.Kind switch
        {
            PersistenceMemberKind.Scalar =>
                $"entity.{member.MemberName}",
            PersistenceMemberKind.Serial =>
                $"(uint)entity.{member.MemberName}",
            PersistenceMemberKind.NestedContract when member.IsNullable =>
                $"entity.{member.MemberName} is null ? null : {member.NestedPersistenceTypeName}.ToSnapshot(entity.{member.MemberName})",
            PersistenceMemberKind.NestedContract =>
                $"{member.NestedPersistenceTypeName}.ToSnapshot(entity.{member.MemberName})",
            PersistenceMemberKind.ScalarCollection =>
                $"entity.{member.MemberName} is null ? [] : [.. entity.{member.MemberName}]",
            PersistenceMemberKind.SerialCollection =>
                $"entity.{member.MemberName} is null ? [] : [.. entity.{member.MemberName}.Select(static value => (uint)value)]",
            PersistenceMemberKind.NestedContractCollection =>
                $"entity.{member.MemberName} is null ? [] : [.. entity.{member.MemberName}.Select({member.NestedPersistenceTypeName}.ToSnapshot)]",
            _ => throw new InvalidOperationException($"Unsupported persistence member kind '{member.Kind}'.")
        };

    private static string BuildFromSnapshotExpression(PersistenceMemberModel member)
        => member.Kind switch
        {
            PersistenceMemberKind.Scalar =>
                $"snapshot.{member.SnapshotMemberName}",
            PersistenceMemberKind.Serial =>
                $"(global::Moongate.UO.Data.Ids.Serial)snapshot.{member.SnapshotMemberName}",
            PersistenceMemberKind.NestedContract when member.IsNullable =>
                $"snapshot.{member.SnapshotMemberName} is null ? null : {member.NestedPersistenceTypeName}.FromSnapshot(snapshot.{member.SnapshotMemberName})",
            PersistenceMemberKind.NestedContract =>
                $"{member.NestedPersistenceTypeName}.FromSnapshot(snapshot.{member.SnapshotMemberName})",
            PersistenceMemberKind.ScalarCollection =>
                $"snapshot.{member.SnapshotMemberName} is null ? [] : [.. snapshot.{member.SnapshotMemberName}]",
            PersistenceMemberKind.SerialCollection =>
                $"snapshot.{member.SnapshotMemberName} is null ? [] : [.. snapshot.{member.SnapshotMemberName}.Select(static value => (global::Moongate.UO.Data.Ids.Serial)value)]",
            PersistenceMemberKind.NestedContractCollection =>
                $"snapshot.{member.SnapshotMemberName} is null ? [] : [.. snapshot.{member.SnapshotMemberName}.Select({member.NestedPersistenceTypeName}.FromSnapshot)]",
            _ => throw new InvalidOperationException($"Unsupported persistence member kind '{member.Kind}'.")
        };

    private static bool TryGetCollectionElement(
        ITypeSymbol typeSymbol,
        out PersistenceCollectionKind collectionKind,
        out ITypeSymbol elementType
    )
    {
        if (typeSymbol is IArrayTypeSymbol arrayType)
        {
            collectionKind = PersistenceCollectionKind.Array;
            elementType = arrayType.ElementType;

            return true;
        }

        if (typeSymbol is INamedTypeSymbol namedType &&
            namedType.IsGenericType &&
            namedType.ConstructedFrom.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ==
            "global::System.Collections.Generic.List<T>")
        {
            collectionKind = PersistenceCollectionKind.List;
            elementType = namedType.TypeArguments[0];

            return true;
        }

        collectionKind = PersistenceCollectionKind.None;
        elementType = null!;

        return false;
    }

    private static bool TryGetNestedPersistenceTypeName(ITypeSymbol typeSymbol, out string persistenceTypeName)
    {
        if (typeSymbol is INamedTypeSymbol namedType && HasPersistedEntityAttribute(namedType))
        {
            persistenceTypeName = GetPersistenceTypeName(namedType);

            return true;
        }

        persistenceTypeName = string.Empty;

        return false;
    }

    private static bool HasPersistedEntityAttribute(INamedTypeSymbol typeSymbol)
        => typeSymbol.GetAttributes()
                     .Any(static current => current.AttributeClass?.ToDisplayString() == PersistedEntityAttributeName);

    private static bool IsSupportedScalar(ITypeSymbol typeSymbol)
    {
        if (typeSymbol.SpecialType != SpecialType.None)
        {
            return typeSymbol.SpecialType != SpecialType.System_Object;
        }

        if (typeSymbol.TypeKind == TypeKind.Enum)
        {
            return true;
        }

        if (typeSymbol is INamedTypeSymbol namedType &&
            namedType.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T)
        {
            return IsSupportedScalar(namedType.TypeArguments[0]);
        }

        return NormalizeTypeName(typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)) switch
        {
            "System.DateTime" => true,
            _ => false
        };
    }

    private static bool IsSerial(ITypeSymbol typeSymbol)
        => typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == SerialTypeName;

    private static string GetSnapshotTypeName(ITypeSymbol typeSymbol)
        => $"{NormalizeTypeName(typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))}Snapshot";

    private static string GetPersistenceTypeName(ITypeSymbol typeSymbol)
        => $"{NormalizeTypeName(typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))}Persistence";

    private static string NormalizeTypeName(string fullyQualifiedName)
        => fullyQualifiedName.StartsWith("global::", StringComparison.Ordinal)
               ? fullyQualifiedName.Substring("global::".Length)
               : fullyQualifiedName;

    private static void AppendNamespaceOpen(StringBuilder builder, string namespaceName)
    {
        if (string.IsNullOrWhiteSpace(namespaceName))
        {
            return;
        }

        builder.Append("namespace ");
        builder.Append(namespaceName);
        builder.AppendLine(";");
        builder.AppendLine();
    }

    private static void AppendNamespaceClose(StringBuilder builder, string namespaceName)
        => _ = namespaceName;
}
