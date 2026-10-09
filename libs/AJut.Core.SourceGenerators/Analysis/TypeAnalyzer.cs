namespace AJut.Text.AJson.SourceGenerators.Analysis
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Collections.Immutable;
    using System.Linq;
    using AJut.Text.AJson.SourceGenerators.Model;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;

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
            Dictionary<string, IPropertySymbol> propertySymbols = new Dictionary<string, IPropertySymbol>();
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
                if (!propertySymbols.ContainsKey(propModel.Name))
                {
                    propertySymbols.Add(propModel.Name, propSymbol);
                }
            }

            // ---- Construction route (AJSON001, AJSON005) ----
            // In order: a non-private parameterless constructor, or a value type's default when it marks no constructor; then the
            //  one constructor marked [AJsonConstructor]; then a record's positional constructor. The reflection path follows the
            //  same rules (AJsonConstructorRoute).
            IMethodSymbol[] markedConstructors = typeSymbol.InstanceConstructors.Where(c => HasAttribute(c, AttributeNames.kAJsonConstructor)).ToArray();
            if (markedConstructors.Length > 1)
            {
                diagnostics.Add(Diagnostic.Create(
                    Diagnostics.MultipleAJsonConstructors,
                    typeSymbol.Locations.FirstOrDefault(),
                    typeSymbol.Name,
                    markedConstructors.Length));
            }

            // Roslyn lists a struct's implicit parameterless constructor too, which is why a value type is decided on its own
            bool buildsWithParameterless = typeSymbol.InstanceConstructors.Any(
                c => c.Parameters.Length == 0
                    && c.DeclaredAccessibility != Accessibility.Private
                    && !(typeSymbol.IsValueType && c.IsImplicitlyDeclared)
            );
            buildsWithParameterless = buildsWithParameterless || (typeSymbol.IsValueType && markedConstructors.Length == 0);

            IMethodSymbol routeConstructor = null;
            if (!buildsWithParameterless)
            {
                if (markedConstructors.Length == 1)
                {
                    routeConstructor = markedConstructors[0];
                }
                else if (markedConstructors.Length == 0 && typeSymbol.IsRecord)
                {
                    routeConstructor = FindPositionalRecordConstructor(typeSymbol, propertyModels);
                }

                // More than one marked constructor already has AJSON005
                if (routeConstructor == null && markedConstructors.Length < 2)
                {
                    diagnostics.Add(Diagnostic.Create(
                        Diagnostics.MissingParameterlessConstructor,
                        typeSymbol.Locations.FirstOrDefault(),
                        typeSymbol.Name));
                }
            }

            IReadOnlyList<ConstructorParameterModel> constructorParameters = routeConstructor == null
                ? Array.Empty<ConstructorParameterModel>()
                : AnalyzeConstructorParameters(typeSymbol, routeConstructor, propertyModels, propertySymbols, diagnostics);

            SerializableTypeModel model = new SerializableTypeModel
            {
                FullyQualifiedTypeName = fullyQualified,
                ContainingNamespace = typeSymbol.ContainingNamespace?.IsGlobalNamespace == false
                    ? typeSymbol.ContainingNamespace.ToDisplayString()
                    : string.Empty,
                MangledName = mangled,
                IsValueType = typeSymbol.IsValueType,
                HasParameterlessConstructor = buildsWithParameterless,
                HasConstructorRoute = routeConstructor != null,
                ConstructsThroughAccessor = routeConstructor != null && routeConstructor.DeclaredAccessibility != Accessibility.Public,
                ConstructorParameters = constructorParameters,
                PropertyAsSelfName = asSelfPropName,
                Properties = propertyModels,
            };

            return new AnalysisResult(model, diagnostics.ToImmutable());
        }

        // ===========================[ Construction route ]===========================
        /// <summary>
        /// A record's positional (primary) constructor. In source it is the constructor the record declaration itself declares.
        /// A record from a referenced assembly has no syntax, so there it is the single public constructor, other than the copy
        /// constructor, whose every parameter matches a property.
        /// </summary>
        private static IMethodSymbol FindPositionalRecordConstructor (INamedTypeSymbol typeSymbol, List<PropertyModel> properties)
        {
            foreach (IMethodSymbol constructor in typeSymbol.InstanceConstructors)
            {
                if (constructor.Parameters.Length == 0 || IsCopyConstructor(constructor, typeSymbol))
                {
                    continue;
                }

                foreach (SyntaxReference reference in constructor.DeclaringSyntaxReferences)
                {
                    if (reference.GetSyntax() is RecordDeclarationSyntax)
                    {
                        return constructor;
                    }
                }
            }

            if (!typeSymbol.DeclaringSyntaxReferences.IsEmpty)
            {
                return null;
            }

            IMethodSymbol found = null;
            foreach (IMethodSymbol constructor in typeSymbol.InstanceConstructors)
            {
                if (constructor.DeclaredAccessibility != Accessibility.Public
                    || constructor.Parameters.Length == 0
                    || IsCopyConstructor(constructor, typeSymbol))
                {
                    continue;
                }

                if (!constructor.Parameters.All(p => FindMatchingProperty(properties, p.Name) != null))
                {
                    continue;
                }

                if (found != null)
                {
                    return null;
                }

                found = constructor;
            }

            return found;
        }

        private static bool IsCopyConstructor (IMethodSymbol constructor, INamedTypeSymbol typeSymbol)
        {
            return constructor.Parameters.Length == 1
                && SymbolEqualityComparer.Default.Equals(constructor.Parameters[0].Type, typeSymbol);
        }

        /// <summary>
        /// Matches each parameter to its property and works out what it is passed when the json has no key for it (AJSON006, AJSON007)
        /// </summary>
        private static List<ConstructorParameterModel> AnalyzeConstructorParameters (
            INamedTypeSymbol typeSymbol,
            IMethodSymbol constructor,
            List<PropertyModel> properties,
            Dictionary<string, IPropertySymbol> propertySymbols,
            ImmutableArray<Diagnostic>.Builder diagnostics)
        {
            List<ConstructorParameterModel> output = new List<ConstructorParameterModel>(constructor.Parameters.Length);
            foreach (IParameterSymbol parameter in constructor.Parameters)
            {
                string parameterFqn = ToFullyQualified(parameter.Type);
                PropertyModel matched = FindMatchingProperty(properties, parameter.Name);
                if (matched == null)
                {
                    diagnostics.Add(Diagnostic.Create(
                        Diagnostics.UnmatchedConstructorParameter,
                        parameter.Locations.FirstOrDefault(),
                        typeSymbol.Name,
                        parameter.Name));
                }

                // A key the json does not have is never an error, since nulls are never written. The value passed is the one the
                //  writer leaves out, when the matched property has [JsonOmitIfDefault]: its explicit value, or the type's default for
                //  the bare attribute. Otherwise the parameter's own default, then the type's default. Each is cast to the parameter's
                //  type, which turns an enum's stored number back into the enum and narrows a number written wider than the
                //  parameter. A default registered with JsonBuilderSettings.RegisterDefaultEquivalent cannot be passed, since the
                //  reader never sees the writer's settings.
                string missingValue;
                if (matched != null && matched.HasOmitIfDefault)
                {
                    bool declaredDefaultDiffers;
                    string omitArgument;
                    string omittedAt;
                    if (matched.HasExplicitOmitDefault)
                    {
                        missingValue = $"({parameterFqn})({matched.ExplicitOmitDefaultLiteral})";
                        declaredDefaultDiffers = parameter.HasExplicitDefaultValue
                            && DefaultsDiffer(GetExplicitOmitValue(propertySymbols[matched.Name]), parameter.ExplicitDefaultValue, parameter.Type);
                        omitArgument = $"({matched.ExplicitOmitDefaultLiteral})";
                        omittedAt = matched.ExplicitOmitDefaultLiteral;
                    }
                    else
                    {
                        missingValue = $"default({parameterFqn})";
                        declaredDefaultDiffers = parameter.HasExplicitDefaultValue && !IsDefaultConstant(parameter.ExplicitDefaultValue);
                        omitArgument = string.Empty;
                        omittedAt = $"default({parameter.Type.ToDisplayString()})";
                    }

                    if (declaredDefaultDiffers)
                    {
                        diagnostics.Add(Diagnostic.Create(
                            Diagnostics.ConstructorDefaultDiffersFromOmitDefault,
                            parameter.Locations.FirstOrDefault(),
                            typeSymbol.Name,
                            parameter.Name,
                            parameter.ExplicitDefaultValue == null ? "null" : FormatConstant(parameter.ExplicitDefaultValue),
                            matched.Name,
                            omitArgument,
                            omittedAt));
                    }
                }
                else if (parameter.HasExplicitDefaultValue && parameter.ExplicitDefaultValue != null)
                {
                    missingValue = $"({parameterFqn})({FormatConstant(parameter.ExplicitDefaultValue)})";
                }
                else
                {
                    missingValue = $"default({parameterFqn})";
                }

                output.Add(new ConstructorParameterModel
                {
                    Name = parameter.Name,
                    TypeFullName = parameterFqn,
                    PropertyName = matched?.Name ?? string.Empty,
                    MissingValueExpression = missingValue,
                });
            }

            return output;
        }

        /// <summary>
        /// The property a constructor parameter fills: the same name, preferring an exact match over one that differs only by case
        /// </summary>
        private static PropertyModel FindMatchingProperty (List<PropertyModel> properties, string parameterName)
        {
            PropertyModel caseInsensitiveMatch = null;
            foreach (PropertyModel property in properties)
            {
                if (property.Name == parameterName)
                {
                    return property;
                }

                if (caseInsensitiveMatch == null && string.Equals(property.Name, parameterName, StringComparison.OrdinalIgnoreCase))
                {
                    caseInsensitiveMatch = property;
                }
            }

            return caseInsensitiveMatch;
        }

        private static object GetExplicitOmitValue (IPropertySymbol propSymbol)
        {
            AttributeData omitAttr = propSymbol.GetAttributes().FirstOrDefault(a => SameAttribute(a, AttributeNames.kOmitIfDefault));
            return omitAttr != null && omitAttr.ConstructorArguments.Length > 0 ? omitAttr.ConstructorArguments[0].Value : null;
        }

        /// <summary>
        /// Whether two constant values end up different once they are the parameter's type. Numbers are compared as that type (an
        /// int 5 and a long 5 are the same, as are 0.1 and 0.1f for a float), and an enum as its stored number.
        /// </summary>
        private static bool DefaultsDiffer (object omitValue, object declaredDefault, ITypeSymbol parameterType)
        {
            if (omitValue == null || declaredDefault == null)
            {
                return (omitValue == null) != (declaredDefault == null);
            }

            if (!IsNumberValue(omitValue) || !IsNumberValue(declaredDefault))
            {
                return !omitValue.Equals(declaredDefault);
            }

            ITypeSymbol target = parameterType;
            if (target is INamedTypeSymbol named && named.ConstructedFrom?.SpecialType == SpecialType.System_Nullable_T)
            {
                target = named.TypeArguments[0];
            }

            switch (target.SpecialType)
            {
                case SpecialType.System_Single:
                    return !((float)Convert.ToDouble(omitValue, CultureInfo.InvariantCulture)).Equals((float)Convert.ToDouble(declaredDefault, CultureInfo.InvariantCulture));
                case SpecialType.System_Double:
                    return !Convert.ToDouble(omitValue, CultureInfo.InvariantCulture).Equals(Convert.ToDouble(declaredDefault, CultureInfo.InvariantCulture));
            }

            // Whole numbers and decimals compare exactly as decimal, which holds every value either can have
            if (!(omitValue is float || omitValue is double || declaredDefault is float || declaredDefault is double))
            {
                return Convert.ToDecimal(omitValue, CultureInfo.InvariantCulture) != Convert.ToDecimal(declaredDefault, CultureInfo.InvariantCulture);
            }

            return !Convert.ToDouble(omitValue, CultureInfo.InvariantCulture).Equals(Convert.ToDouble(declaredDefault, CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Whether a declared parameter default is the type's default, which is what a bare [JsonOmitIfDefault] leaves out
        /// </summary>
        private static bool IsDefaultConstant (object value)
        {
            switch (value)
            {
                case null: return true;
                case bool b: return !b;
                case char c: return c == '\0';
                case float f: return f == 0f;
                case double d: return d == 0d;
            }

            return IsNumberValue(value) && Convert.ToDecimal(value, CultureInfo.InvariantCulture) == 0m;
        }

        private static bool IsNumberValue (object value)
        {
            switch (value)
            {
                case sbyte _:
                case byte _:
                case short _:
                case ushort _:
                case int _:
                case uint _:
                case long _:
                case ulong _:
                case float _:
                case double _:
                case decimal _:
                    return true;
            }

            return false;
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
            bool isInitOnly = hasSetter && propSymbol.SetMethod.IsInitOnly;

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
                IsInitOnly = isInitOnly,
                DeclaringTypeFullName = ToFullyQualified(propSymbol.SetMethod?.ContainingType ?? propSymbol.ContainingType),
                SetterName = propSymbol.SetMethod?.MetadataName ?? string.Empty,
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

            return FormatConstant(arg.Value);
        }

        /// <summary>
        /// A constant (an attribute argument, or a parameter's declared default) as C# source
        /// </summary>
        private static string FormatConstant (object value)
        {
            // Numbers are written invariant, since the generator runs inside the build under whatever culture the build machine has
            //  (2.5 is "2,5" in some), and with the suffix of their own type, since a bare fractional literal is a double and does
            //  not convert to a float in the omit check's EqualityComparer<float>.Equals call.
            switch (value)
            {
                case string s: return SymbolDisplay.FormatLiteral(s, quote: true);
                case char c: return SymbolDisplay.FormatLiteral(c, quote: true);
                case bool b: return b ? "true" : "false";
                case float f: return FormatFloatLiteral(f);
                case double d: return FormatDoubleLiteral(d);
                case decimal m: return m.ToString(CultureInfo.InvariantCulture) + "M";
                case long l: return l.ToString(CultureInfo.InvariantCulture) + "L";
                case ulong ul: return ul.ToString(CultureInfo.InvariantCulture) + "UL";
                case uint ui: return ui.ToString(CultureInfo.InvariantCulture) + "U";
                case IFormattable formattable: return formattable.ToString(null, CultureInfo.InvariantCulture);
            }

            return value.ToString();
        }

        private static string FormatFloatLiteral (float value)
        {
            if (float.IsNaN(value))
            {
                return "float.NaN";
            }
            if (float.IsInfinity(value))
            {
                return value > 0 ? "float.PositiveInfinity" : "float.NegativeInfinity";
            }

            return value.ToString("R", CultureInfo.InvariantCulture) + "F";
        }

        private static string FormatDoubleLiteral (double value)
        {
            if (double.IsNaN(value))
            {
                return "double.NaN";
            }
            if (double.IsInfinity(value))
            {
                return value > 0 ? "double.PositiveInfinity" : "double.NegativeInfinity";
            }

            return value.ToString("R", CultureInfo.InvariantCulture) + "D";
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
