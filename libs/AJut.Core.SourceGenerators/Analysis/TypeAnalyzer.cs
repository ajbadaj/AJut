namespace AJut.Text.AJson.SourceGenerators.Analysis
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using AJut.Text.AJson.SourceGenerators.Model;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;

    /// <summary>
    /// Pure analysis - takes an INamedTypeSymbol, returns a SerializableTypeModel plus any
    /// diagnostics. No source emission, no Roslyn driver dependency. Unit-testable directly
    /// against a hand-built CSharpCompilation.
    /// </summary>
    internal static class TypeAnalyzer
    {
        public sealed record AnalysisResult (SerializableTypeModel Model, ImmutableArray<Diagnostic> Diagnostics);

        public static AnalysisResult Analyze (INamedTypeSymbol typeSymbol)
        {
            ImmutableArray<Diagnostic>.Builder diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
            string fullyQualified = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            string mangled = MangleName(fullyQualified);

            // ---- Constructor analysis (AJSON001) ----
            bool hasParameterlessCtor = typeSymbol.InstanceConstructors.Any(
                c => c.Parameters.Length == 0 && c.DeclaredAccessibility != Accessibility.Private
            );
            bool hasAJsonCtor = typeSymbol.InstanceConstructors.Any(c => HasAttribute(c, AttributeNames.kAJsonConstructor));

            if (!typeSymbol.IsValueType && !hasParameterlessCtor && !hasAJsonCtor)
            {
                diagnostics.Add(Diagnostic.Create(
                    Diagnostics.MissingParameterlessConstructor,
                    typeSymbol.Locations.FirstOrDefault(),
                    typeSymbol.Name));
            }

            // ---- [JsonPropertyAsSelf] on the type ----
            string asSelfPropName = string.Empty;
            AttributeData asSelfAttr = typeSymbol.GetAttributes().FirstOrDefault(
                a => SameAttribute(a, AttributeNames.kPropertyAsSelf)
            );
            if (asSelfAttr != null && asSelfAttr.ConstructorArguments.Length > 0)
            {
                asSelfPropName = asSelfAttr.ConstructorArguments[0].Value as string ?? string.Empty;
            }

            // ---- Property walk ----
            List<PropertyModel> propertyModels = new List<PropertyModel>();
            foreach (IPropertySymbol propSymbol in EnumerateInstanceProperties(typeSymbol))
            {
                if (propSymbol.IsIndexer)
                {
                    continue;
                }
                if (HasAttribute(propSymbol, AttributeNames.kIgnore))
                {
                    continue;
                }
                if (propSymbol.DeclaredAccessibility != Accessibility.Public)
                {
                    continue;
                }

                PropertyModel propModel = AnalyzeProperty(typeSymbol, propSymbol, diagnostics);
                if (propModel == null)
                {
                    continue;
                }

                propertyModels.Add(propModel);
            }

            SerializableTypeModel model = new SerializableTypeModel
            {
                FullyQualifiedTypeName = fullyQualified,
                ContainingNamespace = typeSymbol.ContainingNamespace?.IsGlobalNamespace == false
                    ? typeSymbol.ContainingNamespace.ToDisplayString()
                    : string.Empty,
                MangledName = mangled,
                IsValueType = typeSymbol.IsValueType,
                HasParameterlessConstructor = hasParameterlessCtor,
                HasAJsonConstructor = hasAJsonCtor,
                PropertyAsSelfName = asSelfPropName,
                Properties = propertyModels,
            };

            return new AnalysisResult(model, diagnostics.ToImmutable());
        }

        // ===========================[ Property analysis ]===========================
        private static PropertyModel AnalyzeProperty (
            INamedTypeSymbol owningType,
            IPropertySymbol propSymbol,
            ImmutableArray<Diagnostic>.Builder diagnostics)
        {
            string jsonKey = propSymbol.Name;
            AttributeData aliasAttr = propSymbol.GetAttributes().FirstOrDefault(
                a => SameAttribute(a, AttributeNames.kPropertyAlias)
            );
            if (aliasAttr != null && aliasAttr.ConstructorArguments.Length > 0)
            {
                string aliasValue = aliasAttr.ConstructorArguments[0].Value as string;
                if (!string.IsNullOrEmpty(aliasValue))
                {
                    jsonKey = aliasValue;
                }
            }

            // Nullable<T> unwrap.
            ITypeSymbol declaredType = propSymbol.Type;
            ITypeSymbol underlying = declaredType;
            bool isNullableValueType = false;
            if (declaredType is INamedTypeSymbol named
                && named.IsGenericType
                && named.ConstructedFrom?.SpecialType == SpecialType.System_Nullable_T)
            {
                underlying = named.TypeArguments[0];
                isNullableValueType = true;
            }

            string typeFqn = ToFullyQualified(declaredType);
            string underlyingFqn = ToFullyQualified(underlying);

            bool hasGetter = propSymbol.GetMethod != null && propSymbol.GetMethod.DeclaredAccessibility != Accessibility.Private;
            bool hasSetter = propSymbol.SetMethod != null && propSymbol.SetMethod.DeclaredAccessibility != Accessibility.Private;

            // [JsonRuntimeTypeEval] short-circuits the kind decision.
            AttributeData runtimeAttr = propSymbol.GetAttributes().FirstOrDefault(
                a => SameAttribute(a, AttributeNames.kRuntimeTypeEval)
            );

            ePropertyKind kind;
            string elementFqn = string.Empty;
            string dictKeyFqn = string.Empty;
            string dictValueFqn = string.Empty;
            string runtimeFlagLiteral = string.Empty;

            if (runtimeAttr != null)
            {
                kind = ePropertyKind.RuntimeTypeEval;
                runtimeFlagLiteral = ResolveRuntimeFlagLiteral(runtimeAttr);
            }
            else
            {
                kind = ClassifyType(underlying, out elementFqn, out dictKeyFqn, out dictValueFqn);
                if (kind == ePropertyKind.Unsupported)
                {
                    diagnostics.Add(Diagnostic.Create(
                        Diagnostics.UnsupportedPropertyType,
                        propSymbol.Locations.FirstOrDefault(),
                        owningType.Name,
                        propSymbol.Name,
                        underlyingFqn));
                }
            }

            // [JsonOmitIfDefault] capture + AJSON003 (type mismatch on the explicit value).
            bool hasOmit = false;
            bool hasExplicitOmit = false;
            string explicitLiteral = string.Empty;
            AttributeData omitAttr = propSymbol.GetAttributes().FirstOrDefault(
                a => SameAttribute(a, AttributeNames.kOmitIfDefault)
            );
            if (omitAttr != null)
            {
                hasOmit = true;
                if (omitAttr.ConstructorArguments.Length > 0)
                {
                    TypedConstant arg = omitAttr.ConstructorArguments[0];
                    if (arg.Kind != TypedConstantKind.Error && arg.Value != null)
                    {
                        hasExplicitOmit = true;
                        explicitLiteral = ToLiteralExpression(arg, underlying);

                        // AJSON003 - the literal must be assignable to the property's type. Enums
                        // are stored as their underlying integer; coerce the comparison to the
                        // enum type via Enum.ToObject in the emitted check, but still verify the
                        // attribute argument is the same enum or its underlying numeric type.
                        if (!IsCompatibleExplicitDefault(arg, underlying))
                        {
                            diagnostics.Add(Diagnostic.Create(
                                Diagnostics.OmitIfDefaultTypeMismatch,
                                omitAttr.ApplicationSyntaxReference?.GetSyntax().GetLocation()
                                    ?? propSymbol.Locations.FirstOrDefault(),
                                owningType.Name,
                                propSymbol.Name,
                                arg.Type?.ToDisplayString() ?? "<null>",
                                underlying.ToDisplayString()));
                        }
                    }
                }
            }

            return new PropertyModel
            {
                Name = propSymbol.Name,
                JsonKey = jsonKey,
                TypeFullName = typeFqn,
                UnderlyingTypeFullName = underlyingFqn,
                Kind = kind,
                IsNullable = isNullableValueType,
                IsValueType = underlying.IsValueType,
                HasSetter = hasSetter,
                HasGetter = hasGetter,
                IsUsuallyQuoted = IsUsuallyQuoted(underlying, kind),
                HasOmitIfDefault = hasOmit,
                HasExplicitOmitDefault = hasExplicitOmit,
                ExplicitOmitDefaultLiteral = explicitLiteral,
                ElementTypeFullName = elementFqn,
                DictionaryKeyTypeFullName = dictKeyFqn,
                DictionaryValueTypeFullName = dictValueFqn,
                RuntimeTypeEvalFlagLiteral = runtimeFlagLiteral,
            };
        }

        // ===========================[ Type classification ]===========================
        private static ePropertyKind ClassifyType (
            ITypeSymbol type,
            out string elementFqn,
            out string dictKeyFqn,
            out string dictValueFqn)
        {
            elementFqn = string.Empty;
            dictKeyFqn = string.Empty;
            dictValueFqn = string.Empty;

            if (type.TypeKind == TypeKind.Enum)
            {
                return ePropertyKind.Enum;
            }

            if (IsSimpleSpecialType(type.SpecialType))
            {
                return ePropertyKind.SimpleValue;
            }

            if (IsBuiltInCustomType(type))
            {
                return ePropertyKind.BuiltInCustom;
            }

            // Array
            if (type is IArrayTypeSymbol arrType)
            {
                elementFqn = ToFullyQualified(arrType.ElementType);
                return ePropertyKind.Collection;
            }

            // Generic collections / dictionaries
            if (type is INamedTypeSymbol named && named.IsGenericType)
            {
                INamedTypeSymbol dictIface = FindConstructedInterface(named, "System.Collections.Generic.IDictionary`2");
                if (dictIface != null)
                {
                    dictKeyFqn = ToFullyQualified(dictIface.TypeArguments[0]);
                    dictValueFqn = ToFullyQualified(dictIface.TypeArguments[1]);
                    return ePropertyKind.Dictionary;
                }

                INamedTypeSymbol enumerableIface = FindConstructedInterface(named, "System.Collections.Generic.IEnumerable`1");
                if (enumerableIface != null)
                {
                    elementFqn = ToFullyQualified(enumerableIface.TypeArguments[0]);
                    return ePropertyKind.Collection;
                }
            }

            // Reference types we know how to recurse into are anything not abstract / interface.
            if (type.TypeKind == TypeKind.Class || type.TypeKind == TypeKind.Struct)
            {
                if (type.IsAbstract)
                {
                    return ePropertyKind.Unsupported;
                }
                return ePropertyKind.ComplexReference;
            }

            return ePropertyKind.Unsupported;
        }

        private static bool IsSimpleSpecialType (SpecialType st)
        {
            switch (st)
            {
                case SpecialType.System_String:
                case SpecialType.System_Boolean:
                case SpecialType.System_Char:
                case SpecialType.System_SByte:
                case SpecialType.System_Byte:
                case SpecialType.System_Int16:
                case SpecialType.System_UInt16:
                case SpecialType.System_Int32:
                case SpecialType.System_UInt32:
                case SpecialType.System_Int64:
                case SpecialType.System_UInt64:
                case SpecialType.System_Single:
                case SpecialType.System_Double:
                case SpecialType.System_Decimal:
                    return true;
            }
            return false;
        }

        private static bool IsBuiltInCustomType (ITypeSymbol type)
        {
            string fqn = type.ToDisplayString();
            switch (fqn)
            {
                case "System.DateTime":
                case "System.TimeSpan":
                case "System.Guid":
                case "System.Numerics.Vector2":
                case "System.TimeZoneInfo":
                    return true;
            }
            return false;
        }

        private static bool IsUsuallyQuoted (ITypeSymbol type, ePropertyKind kind)
        {
            if (kind == ePropertyKind.Enum)
            {
                return true;
            }
            if (kind == ePropertyKind.BuiltInCustom)
            {
                return true;
            }
            if (kind == ePropertyKind.SimpleValue)
            {
                switch (type.SpecialType)
                {
                    case SpecialType.System_String:
                    case SpecialType.System_Char:
                        return true;
                }
                return false;
            }
            return false;
        }

        // ===========================[ Helpers ]===========================
        private static IEnumerable<IPropertySymbol> EnumerateInstanceProperties (INamedTypeSymbol typeSymbol)
        {
            // Walk most-derived first, then bases. Within each tier, source order (Roslyn returns
            // members in declaration order). Mirrors TypeMetadataExtensionRegistrar for the
            // attributes-only case; runtime-registered orderings are only honored on the
            // reflection path.
            List<INamedTypeSymbol> chain = new List<INamedTypeSymbol>();
            INamedTypeSymbol current = typeSymbol;
            while (current != null && current.SpecialType != SpecialType.System_Object)
            {
                chain.Add(current);
                current = current.BaseType;
            }

            foreach (INamedTypeSymbol tier in chain)
            {
                foreach (ISymbol member in tier.GetMembers())
                {
                    if (member is IPropertySymbol prop && !prop.IsStatic)
                    {
                        yield return prop;
                    }
                }
            }
        }

        private static bool HasAttribute (ISymbol symbol, string fullyQualifiedAttrName)
        {
            return symbol.GetAttributes().Any(a => SameAttribute(a, fullyQualifiedAttrName));
        }

        private static bool SameAttribute (AttributeData attribute, string fullyQualifiedAttrName)
        {
            INamedTypeSymbol cls = attribute.AttributeClass;
            if (cls == null)
            {
                return false;
            }
            return cls.ToDisplayString() == fullyQualifiedAttrName;
        }

        private static string ToFullyQualified (ITypeSymbol type)
        {
            return type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }

        private static INamedTypeSymbol FindConstructedInterface (INamedTypeSymbol type, string ifaceFqn)
        {
            if (type.ConstructedFrom?.ToDisplayString() == ifaceFqn.Replace("`1", "<>").Replace("`2", "<,>"))
            {
                return type;
            }
            foreach (INamedTypeSymbol iface in type.AllInterfaces)
            {
                INamedTypeSymbol constructed = iface.ConstructedFrom;
                string metadataName = constructed?.MetadataName;
                if (metadataName != null
                    && (metadataName == "IDictionary`2" || metadataName == "IEnumerable`1"))
                {
                    string ns = constructed.ContainingNamespace?.ToDisplayString();
                    if (ns == "System.Collections.Generic"
                        && (constructed.MetadataName == ifaceFqn.Substring(ifaceFqn.LastIndexOf('.') + 1)))
                    {
                        return iface;
                    }
                }
            }
            return null;
        }

        private static string ResolveRuntimeFlagLiteral (AttributeData runtimeAttr)
        {
            if (runtimeAttr.ConstructorArguments.Length == 0)
            {
                return "global::AJut.Text.AJson.eTypeIdInfo.Any";
            }
            TypedConstant arg = runtimeAttr.ConstructorArguments[0];
            if (arg.Kind != TypedConstantKind.Enum)
            {
                return "global::AJut.Text.AJson.eTypeIdInfo.Any";
            }

            // Roslyn stores enum constants as their underlying integer plus the enum type symbol.
            return $"(global::AJut.Text.AJson.eTypeIdInfo){arg.Value}";
        }

        private static string ToLiteralExpression (TypedConstant arg, ITypeSymbol propertyType)
        {
            if (arg.Value == null)
            {
                return "null";
            }

            if (arg.Kind == TypedConstantKind.Enum && propertyType.TypeKind == TypeKind.Enum)
            {
                // Cast the underlying integer back to the property's enum type.
                return $"(global::{propertyType.ToDisplayString()}){arg.Value}";
            }

            switch (arg.Value)
            {
                case string s: return SymbolDisplay.FormatLiteral(s, quote: true);
                case char c: return SymbolDisplay.FormatLiteral(c, quote: true);
                case bool b: return b ? "true" : "false";
            }

            return arg.Value.ToString();
        }

        private static bool IsCompatibleExplicitDefault (TypedConstant arg, ITypeSymbol propertyType)
        {
            if (arg.Type == null || propertyType == null)
            {
                return true;
            }

            // Enum-typed argument must match the property's enum type (or be its underlying integer).
            if (propertyType.TypeKind == TypeKind.Enum)
            {
                if (arg.Type.TypeKind == TypeKind.Enum)
                {
                    return SymbolEqualityComparer.Default.Equals(arg.Type, propertyType);
                }
                INamedTypeSymbol underlying = (propertyType as INamedTypeSymbol)?.EnumUnderlyingType;
                if (underlying != null)
                {
                    return SymbolEqualityComparer.Default.Equals(arg.Type, underlying);
                }
                return false;
            }

            if (SymbolEqualityComparer.Default.Equals(arg.Type, propertyType))
            {
                return true;
            }

            // Allow numeric implicit-convertible cases (int default for a long property, etc.).
            if (IsNumericSpecial(propertyType.SpecialType) && IsNumericSpecial(arg.Type.SpecialType))
            {
                return true;
            }

            return false;
        }

        private static bool IsNumericSpecial (SpecialType st)
        {
            switch (st)
            {
                case SpecialType.System_SByte:
                case SpecialType.System_Byte:
                case SpecialType.System_Int16:
                case SpecialType.System_UInt16:
                case SpecialType.System_Int32:
                case SpecialType.System_UInt32:
                case SpecialType.System_Int64:
                case SpecialType.System_UInt64:
                case SpecialType.System_Single:
                case SpecialType.System_Double:
                case SpecialType.System_Decimal:
                    return true;
            }
            return false;
        }

        private static string MangleName (string fullyQualified)
        {
            // Strip "global::" prefix and replace separators - the result is a legal identifier.
            string trimmed = fullyQualified.StartsWith("global::") ? fullyQualified.Substring("global::".Length) : fullyQualified;
            char[] chars = trimmed.ToCharArray();
            for (int i = 0; i < chars.Length; ++i)
            {
                char c = chars[i];
                if (!(char.IsLetterOrDigit(c) || c == '_'))
                {
                    chars[i] = '_';
                }
            }
            return new string(chars);
        }
    }
}
