namespace AJut.UX.PropertyInteraction
{
    using AJut;
    using AJut.Storage;
    using AJut.Text.AJson;
    using AJut.TypeManagement;
    using AJut.UX;
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Reflection;
    using System.Threading;

    public class PropertyEditTarget : ObservableTreeNode, IExpandableNode
    {
        public delegate object? GetValue();
        public delegate void SetValue(object? value);

        private readonly GetValue m_getValue;
        private readonly SetValue? m_setValue;
        private Func<object, object> m_coerceValue;
        private object m_defaultValue;
        private bool m_hasDefaultValue;
        private PropertyEditTarget m_elevatedChildTarget;

        public PropertyEditTarget(string propertyPathTarget, GetValue getValue, SetValue? setValue = null)
        {
            this.PropertyPathTarget = propertyPathTarget;
            m_getValue = getValue;
            m_setValue = setValue;
        }

        public string PropertyPathTarget { get; }

        public bool IsReadOnly => m_setValue == null;
        public string[] AdditionalEvalTargets { get; set; }

        /// <summary>
        /// Set when either elevation attribute is found on this or a child property.
        /// Non-null means this row is non-expandable; the elevated child's editor shows inline.
        /// Default-value tracking and reset are delegated to the child when this is set.
        /// </summary>
        public PropertyEditTarget ElevatedChildTarget
        {
            get => m_elevatedChildTarget;
            private set
            {
                if (m_elevatedChildTarget != null)
                {
                    m_elevatedChildTarget.PropertyChanged -= this.OnElevatedChildPropertyChanged;
                }

                m_elevatedChildTarget = value;

                if (m_elevatedChildTarget != null)
                {
                    m_elevatedChildTarget.PropertyChanged += this.OnElevatedChildPropertyChanged;
                }
            }
        }

        /// <summary>True when this row should show an inline editor.</summary>
        public bool HasInlineEditor => !this.IsExpandable || this.ElevatedChildTarget != null;

        /// <summary>Returns ElevatedChildTarget if set, otherwise self. Used as ContentControl.Content.</summary>
        public PropertyEditTarget EffectiveEditorTarget => this.ElevatedChildTarget ?? this;

        private bool m_isExpandable;
        public bool IsExpandable
        {
            get => m_isExpandable;
            set
            {
                if (this.SetAndRaiseIfChanged(ref m_isExpandable, value))
                {
                    // Keep IObservableTreeNode.CanHaveChildren in sync so FlatTreeItem
                    // shows/hides the expand toggle correctly.
                    this.CanHaveChildren = value;
                }
            }
        }

        private bool m_isExpanded;

        /// <summary>
        /// Tracks whether this node is expanded in the property tree. Persisted across
        /// <see cref="PropertyGridManager.RebuildEditTargets"/> calls via <see cref="IExpandableNode"/>
        /// integration with <see cref="FlatTreeItem"/>.
        /// </summary>
        public bool IsExpanded
        {
            get => m_isExpanded;
            set => this.SetAndRaiseIfChanged(ref m_isExpanded, value);
        }

        private string m_editor;
        public string Editor
        {
            get => m_editor;
            set => this.SetAndRaiseIfChanged(ref m_editor, value);
        }

        private string m_displayName;
        public string DisplayName
        {
            get => m_displayName;
            set => this.SetAndRaiseIfChanged(ref m_displayName, value);
        }

        private string m_subtitle;
        public string Subtitle
        {
            get => m_subtitle;
            set => this.SetAndRaiseIfChanged(ref m_subtitle, value);
        }

        private string m_iconSource;
        public string IconSource
        {
            get => m_iconSource;
            set => this.SetAndRaiseIfChanged(ref m_iconSource, value);
        }

        private float m_iconMargin;
        public float IconMargin
        {
            get => m_iconMargin;
            set => this.SetAndRaiseIfChanged(ref m_iconMargin, value);
        }

        public string GroupId { get; set; }

        /// <summary>
        /// Raised (as a PropertyChanged notification) after m_setValue commits the new value to the
        /// backing source object. Distinct from "EditValue" which fires inside SetAndRaiseIfChanged
        /// BEFORE m_setValue runs. PropertyGrid subscribes to this name to fire PropertyTreeChanged
        /// only after the source is in sync, preventing the "always one behind" symptom.
        /// </summary>
        public const string SourceCommittedPropertyName = "SourceCommitted";

        private object m_editValue;
        public object EditValue
        {
            get => m_editValue;
            set
            {
                if (this.IsReadOnly)
                {
                    return;
                }

                // Apply attributed coercion (PGCoerce) before caching so the UI
                // displays the coerced value, not the raw editor output.
                object coerced = m_coerceValue != null ? m_coerceValue(value) : value;
                bool wasCoerced = m_coerceValue != null && !Equals(coerced, value);
                if (this.SetAndRaiseIfChanged(ref m_editValue, coerced))
                {
                    m_setValue(coerced);
                    this.UpdateIsAtDefaultValue();
                    this.RaisePropertyChanged(SourceCommittedPropertyName);
                }

                // When coercion changed the value, the binding framework suppresses
                // the synchronous PropertyChanged to avoid re-entrancy. Post a deferred
                // notification so the UI control picks up the coerced value.
                if (wasCoerced)
                {
                    SynchronizationContext.Current?.Post(_ => this.RaisePropertyChanged(nameof(EditValue)), null);
                }
            }
        }

        private object? m_editContext;
        public object? EditContext
        {
            get => m_editContext;
            set => this.SetAndRaiseIfChanged(ref m_editContext, value);
        }

        /// <summary>The default value for this property (CLR default or [PGOverrideDefault]).</summary>
        public object DefaultValue => m_defaultValue;

        /// <summary>True when a meaningful default has been established for this property.</summary>
        public bool HasDefaultValue => m_hasDefaultValue;

        private bool m_isAtDefaultValue;
        /// <summary>
        /// True when EditValue equals DefaultValue. Drives the DefaultValueLabelDataTemplate /
        /// ModifiedValueLabelDataTemplate switch on the PropertyGrid label.
        /// </summary>
        public bool IsAtDefaultValue
        {
            get => m_isAtDefaultValue;
            private set => this.SetAndRaiseIfChanged(ref m_isAtDefaultValue, value);
        }

        /// <summary>Resets EditValue to DefaultValue (no-op if HasDefaultValue is false).
        /// When an elevated child target is present, delegates to that child instead.</summary>
        public void ResetToDefault()
        {
            if (m_elevatedChildTarget != null)
            {
                m_elevatedChildTarget.ResetToDefault();
                return;
            }

            if (m_hasDefaultValue)
            {
                this.EditValue = m_defaultValue;
            }
        }

        public void RecacheEditValue()
        {
            this.SetEditValue(m_getValue());
            m_elevatedChildTarget?.RecacheEditValue();
        }

        /// <summary>
        /// Forces a SourceCommitted notification without going through the EditValue setter.
        /// Used by button targets to signal that sibling property values may have changed.
        /// </summary>
        public void ForceRaiseSourceCommitted()
        {
            this.RaisePropertyChanged(SourceCommittedPropertyName);
        }

        /// <summary>
        /// Unconditionally re-reads the source value and raises PropertyChanged("EditValue")
        /// even if the cached value hasn't changed. Used by NullableEditor to force the inner
        /// editor to refresh when the outer nullable target transitions between null and non-null
        /// states (null→0 and 0→0 would both appear as "no change" to SetAndRaiseIfChanged).
        /// </summary>
        public void ForceRaiseEditValueChanged()
        {
            m_editValue = m_getValue?.Invoke();
            this.RaisePropertyChanged(nameof(EditValue));
            this.UpdateIsAtDefaultValue();
        }

        public bool SetEditValue(object editValue)
        {
            bool changed = this.SetAndRaiseIfChanged(ref m_editValue, editValue, nameof(EditValue));
            if (changed)
            {
                this.UpdateIsAtDefaultValue();
            }

            return changed;
        }

        public bool ShouldEvaluateFor(string propertyPath)
        {
            return this.PropertyPathTarget == propertyPath
                || (this.AdditionalEvalTargets?.Contains(propertyPath) ?? true);
        }

        public override int GetHashCode()
        {
            return this.PropertyPathTarget.GetHashCode();
        }

        /// <summary>
        /// Generates PropertyEditTarget nodes for every public, settable property on <paramref name="sourceItem"/>.
        /// For complex reference-type properties whose current value is non-null, child targets are recursively
        /// generated and attached so the property tree can be expanded in a FlatTreeListControl.
        /// Nullable&lt;T&gt; properties get Editor="Nullable" and EditContext=NullableEditorContext.
        /// </summary>
        public static IEnumerable<PropertyEditTarget> GenerateForPropertiesOf(object sourceItem)
        {
            return GenerateForPropertiesOf(sourceItem, depth: 0);
        }

        private static IEnumerable<PropertyEditTarget> GenerateForPropertiesOf(object sourceItem, int depth)
        {
            PropertyEditTarget[] _pendingCoerceHolder = null;
            foreach (PropertyInfo prop in _GetRelevantProperties(sourceItem))
            {
                var editorAttr = TypeMetadataExtensionRegistrar.GetAttribute<PGEditorAttribute>(prop);
                var labelAttr = TypeMetadataExtensionRegistrar.GetAttribute<PGLabelAttribute>(prop);
                string displayName = labelAttr?.Label
                    ?? TypeMetadataExtensionRegistrar.GetAttribute<DisplayNameAttribute>(prop)?.DisplayName;
                string subtitle = labelAttr?.Subtitle;
                string[] aliases = TypeMetadataExtensionRegistrar.GetAttribute<PGAltPropertyAliasAttribute>(prop)?.AltPropertyAliases;

                // 1. Detect Nullable<T> and route to the "Nullable" editor
                Type underlyingNullable = Nullable.GetUnderlyingType(prop.PropertyType);
                bool isNullable = underlyingNullable != null;

                // 2. Detect [PGTypeAlias] for type-converting editors (e.g. a type alias to a different editor).
                //    Ignored when [PGEditor] or Nullable unwrapping already applies.
                var aliasAttr = TypeMetadataExtensionRegistrar.GetAttribute<PGTypeAliasAttribute>(prop);
                IPropertyGridTypeAliasing? aliasing = (aliasAttr != null && !isNullable && editorAttr == null)
                    ? (IPropertyGridTypeAliasing)Activator.CreateInstance(aliasAttr.AliasingType)
                    : null;

                string editorKey;
                object editContext = null;
                if (isNullable && editorAttr == null)
                {
                    editorKey = "Nullable";
                    editContext = new NullableEditorContext(underlyingNullable.Name, underlyingNullable);
                }
                else if (aliasing != null)
                {
                    editorKey = aliasing.AliasType.Name;
                }
                else
                {
                    editorKey = editorAttr?.Editor ?? prop.PropertyType.Name;
                }

                GetValue getter = aliasing != null
                    ? () => aliasing.ConvertToAlias(prop.GetValue(sourceItem))
                    : () => prop.GetValue(sourceItem);

                // 2b. Build setter and optional [PGCoerce] delegate
                SetValue setter;
                Func<object, object> coerceDelegate = null;
                if (aliasing != null)
                {
                    setter = v => prop.SetValue(sourceItem, aliasing.ConvertFromAlias(v));
                }
                else
                {
                    var coerceAttr = TypeMetadataExtensionRegistrar.GetAttribute<PGCoerceAttribute>(prop);
                    MethodInfo coerceMethod = coerceAttr != null
                        ? _ResolveCoerceMethod(sourceItem.GetType(), coerceAttr.MemberName)
                        : null;

                    if (coerceMethod != null)
                    {
                        // Mutable holder for the PropertyEditTarget reference (set after construction)
                        var targetHolder = new PropertyEditTarget[1];
                        var coerceParams = coerceMethod.GetParameters();
                        if (coerceParams.Length == 2 && coerceParams[1].ParameterType == typeof(PropertyEditTarget))
                        {
                            coerceDelegate = v => coerceMethod.Invoke(
                                coerceMethod.IsStatic ? null : sourceItem,
                                new object[] { v, targetHolder[0] }
                            );
                        }
                        else
                        {
                            coerceDelegate = v => coerceMethod.Invoke(
                                coerceMethod.IsStatic ? null : sourceItem,
                                new object[] { v }
                            );
                        }

                        // Setter is plain - coercion is applied in EditValue before this runs
                        setter = v => prop.SetValue(sourceItem, v);
                        _pendingCoerceHolder = targetHolder;
                    }
                    else
                    {
                        _pendingCoerceHolder = null;
                        setter = v => prop.SetValue(sourceItem, _CoerceValueType(v, prop.PropertyType));
                    }
                }

                // 3. Build EditContext from [PGEditContextBuilder] if present and no context was set above
                if (editContext == null)
                {
                    var ctxBuilderAttr = TypeMetadataExtensionRegistrar.GetAttribute<PGEditContextBuilderAttribute>(prop);
                    if (ctxBuilderAttr != null)
                    {
                        editContext = _BuildEditContext(ctxBuilderAttr);
                    }
                }

                var groupAttr = TypeMetadataExtensionRegistrar.GetAttribute<PGGroupAttribute>(prop);

                var target = new PropertyEditTarget(prop.Name, getter, setter)
                {
                    DisplayName = displayName ?? prop.Name.ConvertToFriendlyEn(),
                    Subtitle = subtitle,
                    IconSource = labelAttr?.IconSource,
                    IconMargin = labelAttr?.IconMargin ?? 0f,
                    GroupId = groupAttr?.GroupId,
                    Editor = editorKey,
                    EditContext = editContext,
                    AdditionalEvalTargets = aliases,
                };

                // 3b. Complete deferred coerce holder and coercion delegate assignment
                if (_pendingCoerceHolder != null)
                {
                    _pendingCoerceHolder[0] = target;
                    _pendingCoerceHolder = null;
                    target.m_coerceValue = coerceDelegate;
                }

                // 4. Compute default value
                _ApplyDefault(target, prop, sourceItem);

                // 5. Recurse into complex reference types (non-nullable, non-aliased, non-string, non-enum,
                // non-value-type) that have a non-null value and editable sub-properties. Cap recursion at depth 5.
                if (!isNullable && aliasing == null && depth < 5 && _IsComplexObjectType(prop.PropertyType))
                {
                    object? subValue = prop.GetValue(sourceItem);
                    if (subValue != null && _GetRelevantProperties(subValue).Any())
                    {
                        // Check for [PGElevateChildProperty("X")] on the property itself.
                        var elevateChildAttr = TypeMetadataExtensionRegistrar.GetAttribute<PGElevateChildPropertyAttribute>(prop);
                        if (elevateChildAttr != null)
                        {
                            PropertyInfo? childProp = prop.PropertyType.GetProperty(elevateChildAttr.ChildPropertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.GetProperty);
                            if (childProp != null)
                            {
                                var childCtxAttr = TypeMetadataExtensionRegistrar.GetAttribute<PGEditContextBuilderAttribute>(childProp);
                                object childEditContext = childCtxAttr != null ? _BuildEditContext(childCtxAttr) : null;

                                var childTarget = new PropertyEditTarget(
                                    childProp.Name,
                                    () => childProp.GetValue(prop.GetValue(sourceItem)),
                                    childProp.SetMethod != null ? v => childProp.SetValue(prop.GetValue(sourceItem), _CoerceValueType(v, childProp.PropertyType)) : (SetValue?)null
                                )
                                {
                                    DisplayName = _GetDisplayName(childProp, prop.Name.ConvertToFriendlyEn()),
                                    Editor = _GetEditorKey(childProp, childProp.PropertyType.Name),
                                    EditContext = childEditContext,
                                };
                                _ApplyDefault(childTarget, childProp, subValue);
                                childTarget.Setup();
                                target.ElevatedChildTarget = childTarget;
                                target.IsExpandable = false;
                            }
                        }
                        else
                        {
                            // Check if any property of the sub-type has [PGElevateAsParent].
                            PropertyInfo? elevatedProp = prop.PropertyType
                                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.GetProperty)
                                .FirstOrDefault(p => TypeMetadataExtensionRegistrar.HasAttribute<PGElevateAsParentAttribute>(p));
                            if (elevatedProp != null)
                            {
                                var elevateToParentAttr = TypeMetadataExtensionRegistrar.GetAttribute<PGElevateAsParentAttribute>(elevatedProp);
                                PropertyInfo attributeSourceProperty = elevateToParentAttr.DeferPGAttributesToParent ? prop : elevatedProp;

                                // Resolve EditContext for the elevated target: when deferring to
                                // the parent property, reuse the already-built context; otherwise
                                // check the elevated property for its own PGEditContextBuilder.
                                object elevatedEditContext;
                                if (elevateToParentAttr.DeferPGAttributesToParent)
                                {
                                    elevatedEditContext = editContext;
                                }
                                else
                                {
                                    var elevCtxAttr = TypeMetadataExtensionRegistrar.GetAttribute<PGEditContextBuilderAttribute>(elevatedProp);
                                    elevatedEditContext = elevCtxAttr != null ? _BuildEditContext(elevCtxAttr) : null;
                                }

                                var childTarget = new PropertyEditTarget(
                                    elevatedProp.Name,
                                    () => elevatedProp.GetValue(prop.GetValue(sourceItem)),
                                    elevatedProp.SetMethod != null ? v => elevatedProp.SetValue(prop.GetValue(sourceItem), _CoerceValueType(v, elevatedProp.PropertyType)) : (SetValue?)null
                                )
                                {
                                    DisplayName = _GetDisplayName(attributeSourceProperty, $"{prop.Name.ConvertToFriendlyEn()}+{elevatedProp.Name.ConvertToFriendlyEn()}"),
                                    Editor = _GetEditorKey(attributeSourceProperty, elevatedProp.PropertyType.Name),
                                    EditContext = elevatedEditContext,
                                };
                                _ApplyDefault(childTarget, elevatedProp, subValue);
                                childTarget.Setup();
                                target.ElevatedChildTarget = childTarget;
                                target.IsExpandable = false;
                            }
                            else
                            {
                                // Normal expandable sub-object.
                                target.IsExpandable = true;
                                foreach (PropertyEditTarget child in GenerateForPropertiesOf(subValue, depth + 1))
                                {
                                    child.Setup();
                                    target.InsertChild(target.Children.Count, child);
                                }
                            }
                        }
                    }
                }

                yield return target;
            }

            string _GetDisplayName(PropertyInfo prop, string fallback)
            {
                return TypeMetadataExtensionRegistrar.GetAttribute<DisplayNameAttribute>(prop)?.DisplayName ?? fallback;
            }

            string _GetEditorKey(PropertyInfo prop, string fallback)
            {
                return TypeMetadataExtensionRegistrar.GetAttribute<PGEditorAttribute>(prop)?.Editor ?? fallback;
            }

            IEnumerable<PropertyInfo> _GetRelevantProperties(object _item)
            {
                bool showReadOnly = _item.GetType().IsTaggedWithAttribute<PGShowReadonlyAttribute>();
                return TypeMetadataExtensionRegistrar.GetOrderedProperties(
                    _item.GetType(),
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.GetProperty
                ).Where(_Filter);

                bool _Filter(PropertyInfo _prop)
                {
                    // Global registry hide takes priority, then [PGHidden] attribute.
                    if (TypeMetadataExtensionRegistrar.IsHidden(_prop)
                        || TypeMetadataExtensionRegistrar.HasAttribute<PGHiddenAttribute>(_prop))
                    {
                        return false;
                    }

                    // ShowIf/HideIf filtering is handled by PropertyGridManager after
                    // tree construction - targets are always generated so they can be
                    // toggled in/out without a full rebuild.

                    if (showReadOnly)
                    {
                        return true;
                    }

                    return _prop.SetMethod != null
                        || TypeMetadataExtensionRegistrar.HasAttribute<PGShowReadonlyAttribute>(_prop);
                }
            }
        }

        /// <summary>
        /// Evaluates a boolean member (property or method) on the source item.
        /// Methods may optionally take a single <see cref="PropertyEditTarget"/> parameter.
        /// Returns false if the member is not found or does not return a boolean.
        /// </summary>
        internal static bool EvaluateBoolMember(object sourceItem, string memberName, PropertyEditTarget target = null)
        {
            Type type = sourceItem.GetType();

            // 1. Try property
            PropertyInfo prop = type.GetProperty(
                memberName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static
            );
            if (prop != null)
            {
                object instance = (prop.GetGetMethod(true)?.IsStatic == true) ? null : sourceItem;
                return prop.GetValue(instance) is bool b && b;
            }

            // 2. Try zero-parameter method
            MethodInfo method = type.GetMethod(
                memberName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static,
                null, Type.EmptyTypes, null
            );

            // 3. Try one-parameter method (PropertyEditTarget)
            if (method == null)
            {
                method = type.GetMethod(
                    memberName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static,
                    null, new[] { typeof(PropertyEditTarget) }, null
                );
            }

            if (method != null)
            {
                object instance = method.IsStatic ? null : sourceItem;
                var parameters = method.GetParameters();
                object result;
                if (parameters.Length == 1 && parameters[0].ParameterType == typeof(PropertyEditTarget))
                {
                    result = method.Invoke(instance, new object[] { target });
                }
                else
                {
                    result = method.Invoke(instance, null);
                }

                return result is bool bResult && bResult;
            }

            Logger.LogError($"PGShowIf/PGHideIf: member '{memberName}' not found on '{type.Name}'");
            return false;
        }

        /// <summary>
        /// Scans the source item's methods for [PGButton] and generates a PropertyEditTarget
        /// for each. The target's Editor is "Button", EditValue is the button label, and
        /// EditContext is an <see cref="ActionCommand"/> that invokes the method.
        /// </summary>
        internal static IEnumerable<PropertyEditTarget> GenerateButtonsForMethodsOf(object sourceItem)
        {
            Type type = sourceItem.GetType();
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                var buttonAttr = method.GetCustomAttribute<PGButtonAttribute>();
                if (buttonAttr == null)
                {
                    continue;
                }

                // ShowIf/HideIf filtering is handled by PropertyGridManager after
                // tree construction - button targets are always generated so they can
                // be toggled in/out without a full rebuild.

                string buttonName = !string.IsNullOrEmpty(buttonAttr.ButtonName)
                    ? buttonAttr.ButtonName
                    : method.Name.ConvertToFriendlyEn();

                var capturedMethod = method;
                var capturedItem = sourceItem;

                var groupAttr = method.GetCustomAttribute<PGGroupAttribute>();
                var target = new PropertyEditTarget(method.Name, () => buttonName, null)
                {
                    DisplayName = buttonName,
                    Editor = "Button",
                    GroupId = groupAttr?.GroupId,
                };

                // Capture target so the action can signal SourceCommitted after running
                var capturedTarget = target;
                Action action = () =>
                {
                    capturedMethod.Invoke(capturedMethod.IsStatic ? null : capturedItem, null);
                    capturedTarget.ForceRaiseSourceCommitted();
                };
                target.EditContext = new ActionCommand(action);

                yield return target;
            }
        }

        /// <summary>
        /// Returns true if the source type has any properties with [PGShowIf], [PGHideIf],
        /// or methods with [PGButton] + visibility attributes. Used by PropertyGridManager
        /// to decide whether to re-evaluate visibility on property changes.
        /// </summary>
        internal static bool HasConditionalVisibility(object sourceItem)
        {
            Type type = sourceItem.GetType();

            foreach (PropertyInfo prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (TypeMetadataExtensionRegistrar.HasAttribute<PGShowIfAttribute>(prop)
                    || TypeMetadataExtensionRegistrar.HasAttribute<PGHideIfAttribute>(prop))
                {
                    return true;
                }
            }

            foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                if (method.GetCustomAttribute<PGButtonAttribute>() != null
                    && (method.GetCustomAttribute<PGShowIfAttribute>() != null
                        || method.GetCustomAttribute<PGHideIfAttribute>() != null))
                {
                    return true;
                }
            }

            return false;
        }

        private static MethodInfo _ResolveCoerceMethod(Type type, string memberName)
        {
            foreach (MethodInfo m in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                if (m.Name != memberName)
                {
                    continue;
                }

                var parameters = m.GetParameters();
                if (parameters.Length == 1
                    || (parameters.Length == 2 && parameters[1].ParameterType == typeof(PropertyEditTarget)))
                {
                    return m;
                }
            }

            Logger.LogError($"PGCoerce: method '{memberName}' with signature (object) or (object, PropertyEditTarget) not found on '{type.Name}'");
            return null;
        }

        private static void _ApplyDefault(PropertyEditTarget target, PropertyInfo prop, object sourceItem)
        {
            var overrideAttr = TypeMetadataExtensionRegistrar.GetAttribute<PGOverrideDefaultAttribute>(prop);
            if (overrideAttr != null)
            {
                if (overrideAttr.IsMethodBased)
                {
                    // Locate the method (public or non-public, instance or static, zero parameters).
                    MethodInfo method = sourceItem?.GetType().GetMethod(
                        overrideAttr.MethodName,
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static,
                        null,
                        Type.EmptyTypes,
                        null
                    );

                    if (method != null)
                    {
                        try
                        {
                            target.m_defaultValue = method.Invoke(method.IsStatic ? null : sourceItem, null);
                            target.m_hasDefaultValue = true;
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"[WARNING] PGOverrideDefault method '{overrideAttr.MethodName}' threw during default value resolution", ex);
                        }
                    }
                    else
                    {
                        Logger.LogError($"[WARNING] PGOverrideDefault: method '{overrideAttr.MethodName}' not found on type '{sourceItem?.GetType().Name}'");
                    }
                }
                else
                {
                    target.m_defaultValue = overrideAttr.FixedDefaultValue;
                    target.m_hasDefaultValue = true;
                }
            }
            else
            {
                // Natural CLR default: null for reference/nullable types, default(T) for value types.
                target.m_defaultValue = prop.PropertyType.IsValueType ? Activator.CreateInstance(prop.PropertyType) : null;
                target.m_hasDefaultValue = true;
            }
        }

        private static object _CoerceValueType(object value, Type targetType)
        {
            if (value == null || targetType.IsInstanceOfType(value))
            {
                return value;
            }

            try
            {
                return Convert.ChangeType(value, targetType);
            }
            catch
            {
                return value;
            }
        }

        private static bool _IsComplexObjectType(Type type)
        {
            return !type.IsValueType
                && type != typeof(string)
                && !type.IsEnum
                && !type.IsArray
                && !typeof(System.Collections.IEnumerable).IsAssignableFrom(type);
        }

        private static object _BuildEditContext(PGEditContextBuilderAttribute attr)
        {
            if (!TypeIdRegistrar.TryGetType(attr.TypeId, out Type contextType))
            {
                Logger.LogError($"PGEditContextBuilder: TypeId '{attr.TypeId}' not found in TypeIdRegistrar. Ensure the assembly containing this type has been registered.");
                return null;
            }

            try
            {
                Json parsed = JsonHelper.ParseText(attr.Json);
                if (parsed.HasErrors)
                {
                    Logger.LogError($"PGEditContextBuilder: Failed to parse JSON for TypeId '{attr.TypeId}': {parsed.GetErrorReport()}");
                    return null;
                }

                return JsonHelper.BuildObjectForJson(contextType, parsed);
            }
            catch (Exception ex)
            {
                Logger.LogError($"PGEditContextBuilder: Failed to deserialize JSON for TypeId '{attr.TypeId}'", ex);
                return null;
            }
        }

        public void TakeOn(PropertyEditTarget target)
        {
            // Intentionally empty - merging of duplicate targets not yet implemented.
        }

        public void Setup()
        {
            if (this.DisplayName.IsNullOrEmpty())
            {
                this.DisplayName = this.PropertyPathTarget.ConvertToFriendlyEn();
            }

            if (this.EditValue == null)
            {
                m_editValue = m_getValue?.Invoke();
            }

            this.UpdateIsAtDefaultValue();
        }

        private void UpdateIsAtDefaultValue()
        {
            if (m_elevatedChildTarget != null)
            {
                this.IsAtDefaultValue = m_elevatedChildTarget.IsAtDefaultValue;
                return;
            }

            this.IsAtDefaultValue = m_hasDefaultValue && object.Equals(m_editValue, m_defaultValue);
        }

        private void OnElevatedChildPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IsAtDefaultValue))
            {
                this.IsAtDefaultValue = m_elevatedChildTarget.IsAtDefaultValue;
            }
        }
    }
}
