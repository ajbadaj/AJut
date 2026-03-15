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
    using System.Text.RegularExpressions;
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
        private INotifyPropertyChanged m_elevatedSubObject;
        private INotifyPropertyChanged m_editValueINPC;

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

        /// <summary>True when this target represents a list/array/collection parent with [PGList].</summary>
        public bool IsListEditor { get; set; }

        /// <summary>True when this target is a child element of a list property.</summary>
        public bool IsListElement => this.EditContext is PropertyGridListElementContext;

        /// <summary>True when this is a removable list element (non-null only when IsListElement and CanRemove).</summary>
        public bool CanRemoveFromList => this.EditContext is PropertyGridListElementContext { CanRemove: true };

        /// <summary>The remove command for list elements (null when not a removable list element).</summary>
        public System.Windows.Input.ICommand ListElementRemoveCommand => (this.EditContext as PropertyGridListElementContext)?.RemoveCommand;

        /// <summary>True when this row should show an inline editor.</summary>
        public bool HasInlineEditor => !this.IsExpandable || this.ElevatedChildTarget != null || this.IsListEditor;

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
        /// The object on which ShowIf/HideIf condition members are evaluated.
        /// Set during GenerateForPropertiesOf / GenerateButtonsForMethodsOf so that
        /// PropertyGridManager can link conditions even when the PropertyGrid source
        /// is an IPropertyEditManager that delegates to a different object.
        /// </summary>
        internal object ConditionSourceItem { get; set; }

        /// <summary>Attached during target generation so PropertyGridManager can link conditions from the target itself.</summary>
        internal PGShowIfAttribute PendingShowIf { get; set; }

        /// <summary>Attached during target generation so PropertyGridManager can link conditions from the target itself.</summary>
        internal PGHideIfAttribute PendingHideIf { get; set; }

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

        public override string ToString () => this.DisplayName ?? base.ToString();

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
        public static IEnumerable<PropertyEditTarget> GenerateForPropertiesOf(object sourceItem, int maxDepth = 5, params string[] limtToTheseProperties)
        {
            return GenerateForPropertiesOf(sourceItem, depth: 0, maxDepth: maxDepth, limtToTheseProperties.Select(s => new PropertyMatcher(s)).ToArray());
        }

        private static IEnumerable<PropertyEditTarget> GenerateForPropertiesOf(object sourceItem, int depth, int maxDepth, PropertyMatcher[] limitToTheseProperties)
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
                        ? ResolveCoerceMethod(sourceItem.GetType(), coerceAttr.MemberName)
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
                        setter = v => prop.SetValue(sourceItem, CoerceValueType(v, prop.PropertyType));
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

                // 3c. Attach ShowIf/HideIf condition info so PropertyGridManager can
                //     link conditions from the target itself (required when the PropertyGrid
                //     source is an IPropertyEditManager that delegates to a different object).
                var showIfAttr = TypeMetadataExtensionRegistrar.GetAttribute<PGShowIfAttribute>(prop);
                var hideIfAttr = TypeMetadataExtensionRegistrar.GetAttribute<PGHideIfAttribute>(prop);
                if (showIfAttr != null || hideIfAttr != null)
                {
                    target.PendingShowIf = showIfAttr;
                    target.PendingHideIf = hideIfAttr;
                    target.ConditionSourceItem = sourceItem;
                }

                // 4. Compute default value
                ApplyDefault(target, prop, sourceItem);

                // 4b. Detect [PGList] on list/array/collection properties and generate
                //     an expandable parent with inline editor + element children.
                var listAttr = TypeMetadataExtensionRegistrar.GetAttribute<PGListAttribute>(prop);
                if (listAttr != null && IsListType(prop.PropertyType, out Type listElementType))
                {
                    target.Editor = "List";
                    target.IsListEditor = true;
                    target.IsExpandable = true;

                    // Capture for lambda closures
                    var capturedTarget = target;
                    var capturedProp = prop;
                    var capturedSourceItem = sourceItem;
                    var capturedElementType = listElementType;
                    var capturedListAttr = listAttr;
                    int capturedDepth = depth;
                    int capturedMaxDepth = maxDepth;

                    var listContext = new PropertyGridListContext(
                        capturedSourceItem,
                        capturedProp,
                        capturedElementType,
                        capturedListAttr,
                        () => RebuildListChildren(capturedTarget, capturedSourceItem, capturedProp, capturedElementType, capturedListAttr, capturedDepth, capturedMaxDepth, limitToTheseProperties)
                    );

                    target.EditContext = listContext;

                    // Generate initial children for existing elements
                    BuildListChildren(target, sourceItem, prop, listElementType, listAttr, depth, maxDepth, limitToTheseProperties);

                    yield return target;
                    continue;
                }

                // 5. For complex reference types, check for elevation attributes (which work even with
                // [PGEditor]), and fall back to normal sub-property recursion when no elevation is found
                // and no [PGEditor] overrides the type.
                if (!isNullable && aliasing == null && depth < maxDepth && IsComplexObjectType(prop.PropertyType))
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
                                    childProp.SetMethod != null ? v => childProp.SetValue(prop.GetValue(sourceItem), CoerceValueType(v, childProp.PropertyType)) : (SetValue?)null
                                )
                                {
                                    DisplayName = _GetDisplayName(childProp, prop.Name.ConvertToFriendlyEn()),
                                    Editor = _GetEditorKey(childProp, childProp.PropertyType.Name),
                                    EditContext = childEditContext,
                                };
                                ApplyDefault(childTarget, childProp, subValue);
                                childTarget.Setup();
                                target.ElevatedChildTarget = childTarget;
                                target.IsExpandable = false;
                                target.WireUpElevatedSubObjectINPC(subValue);
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
                                    elevatedProp.SetMethod != null ? v => elevatedProp.SetValue(prop.GetValue(sourceItem), CoerceValueType(v, elevatedProp.PropertyType)) : (SetValue?)null
                                )
                                {
                                    DisplayName = _GetDisplayName(attributeSourceProperty, $"{prop.Name.ConvertToFriendlyEn()}+{elevatedProp.Name.ConvertToFriendlyEn()}"),
                                    Editor = _GetEditorKey(attributeSourceProperty, elevatedProp.PropertyType.Name),
                                    EditContext = elevatedEditContext,
                                };
                                ApplyDefault(childTarget, elevatedProp, subValue);
                                childTarget.Setup();
                                target.ElevatedChildTarget = childTarget;
                                target.IsExpandable = false;
                                target.WireUpElevatedSubObjectINPC(subValue);
                            }
                            else if (editorAttr == null)
                            {
                                // Normal expandable sub-object - only when no [PGEditor] overrides the type.
                                target.IsExpandable = true;
                                foreach (PropertyEditTarget child in GenerateForPropertiesOf(subValue, depth + 1, maxDepth, limitToTheseProperties))
                                {
                                    child.Setup();
                                    target.InsertChild(target.Children.Count, child);
                                }
                            }
                            else if (subValue is INotifyPropertyChanged)
                            {
                                // [PGEditor] with no elevation: the template binds into
                                // sub-properties of EditValue. Wire up INPC so those writes
                                // raise SourceCommitted.
                                target.WireUpEditValueINPC(subValue);
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
                    if (limitToTheseProperties.Length > 0 && !limitToTheseProperties.Any(tester => tester.IsMatch(_prop)))
                    {
                        return false;
                    }

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

            // 1. Try property (walks hierarchy via GetProperty with NonPublic)
            PropertyInfo prop = type.GetProperty(
                memberName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static
            );
            if (prop != null)
            {
                object instance = (prop.GetGetMethod(true)?.IsStatic == true) ? null : sourceItem;
                return prop.GetValue(instance) is bool b && b;
            }

            // 2. Try zero-parameter method, walking base types for private methods
            MethodInfo method = _FindMethod(type, memberName, Type.EmptyTypes);

            // 3. Try one-parameter method (PropertyEditTarget)
            if (method == null)
            {
                method = _FindMethod(type, memberName, new[] { typeof(PropertyEditTarget) });
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
        /// Walks the type hierarchy to find a method, including private methods on base classes
        /// which Type.GetMethod with NonPublic does not return for derived types.
        /// </summary>
        private static MethodInfo _FindMethod (Type type, string name, Type[] parameterTypes)
        {
            const BindingFlags kFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            Type current = type;
            while (current != null && current != typeof(object))
            {
                MethodInfo method = current.GetMethod(name, kFlags | BindingFlags.DeclaredOnly, null, parameterTypes, null);
                if (method != null)
                {
                    return method;
                }

                current = current.BaseType;
            }

            return null;
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

                // Attach ShowIf/HideIf condition info for PropertyGridManager
                var buttonShowIf = method.GetCustomAttribute<PGShowIfAttribute>();
                var buttonHideIf = method.GetCustomAttribute<PGHideIfAttribute>();
                if (buttonShowIf != null || buttonHideIf != null)
                {
                    target.PendingShowIf = buttonShowIf;
                    target.PendingHideIf = buttonHideIf;
                    target.ConditionSourceItem = sourceItem;
                }

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

        private static MethodInfo ResolveCoerceMethod(Type type, string memberName)
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

        private static void ApplyDefault(PropertyEditTarget target, PropertyInfo prop, object sourceItem)
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

        private static object CoerceValueType(object value, Type targetType)
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

        private static bool IsComplexObjectType(Type type)
        {
            return !type.IsValueType
                && type != typeof(string)
                && !type.IsEnum
                && !type.IsArray
                && !typeof(System.Collections.IEnumerable).IsAssignableFrom(type);
        }

        /// <summary>
        /// Returns true if the type is a supported list/array/collection type for [PGList],
        /// and outputs the element type.
        /// </summary>
        private static bool IsListType (Type type, out Type elementType)
        {
            // T[]
            if (type.IsArray)
            {
                elementType = type.GetElementType();
                return elementType != null;
            }

            // IList<T>, List<T>, ICollection<T>, IEnumerable<T>
            foreach (Type iface in type.GetInterfaces())
            {
                if (iface.IsGenericType)
                {
                    Type def = iface.GetGenericTypeDefinition();
                    if (def == typeof(IList<>) || def == typeof(ICollection<>) || def == typeof(IEnumerable<>))
                    {
                        elementType = iface.GetGenericArguments()[0];
                        return true;
                    }
                }
            }

            // Check the type itself (e.g. List<T> directly)
            if (type.IsGenericType)
            {
                Type def = type.GetGenericTypeDefinition();
                if (def == typeof(List<>) || def == typeof(IList<>) || def == typeof(ICollection<>))
                {
                    elementType = type.GetGenericArguments()[0];
                    return true;
                }
            }

            // Non-generic IList (ArrayList, etc.)
            if (typeof(System.Collections.IList).IsAssignableFrom(type))
            {
                elementType = typeof(object);
                return true;
            }

            elementType = null;
            return false;
        }

        /// <summary>
        /// Builds child PropertyEditTarget nodes for each element in a list property.
        /// Called during initial generation and on rebuild after add/remove.
        /// Cascades [PGEditor], [PGElevateChildProperty], and respects [PGElevateAsParent]
        /// on the element type so list children behave like normal complex-property rows.
        /// </summary>
        private static void BuildListChildren (
            PropertyEditTarget listParent,
            object sourceItem,
            PropertyInfo prop,
            Type elementType,
            PGListAttribute listAttr,
            int depth,
            int maxDepth,
            PropertyMatcher[] limitToTheseProperties)
        {
            object collection = prop.GetValue(sourceItem);
            if (collection == null)
            {
                return;
            }

            var listContext = listParent.EditContext as PropertyGridListContext;
            bool isSimpleType = elementType.IsValueType || elementType == typeof(string) || elementType.IsEnum;

            // ------ Read cascading attributes from the list property ------

            // [PGEditor] on the list: each element uses that editor, no recursion.
            var listEditorAttr = TypeMetadataExtensionRegistrar.GetAttribute<PGEditorAttribute>(prop);
            string overrideEditor = listEditorAttr?.Editor;

            // [PGElevateChildProperty] on the list: elevate a named child of each element inline.
            var elevateChildAttr = (overrideEditor == null)
                ? TypeMetadataExtensionRegistrar.GetAttribute<PGElevateChildPropertyAttribute>(prop)
                : null;

            // [PGElevateAsParent] on the element type: discover once, apply to every element.
            PropertyInfo elevateAsParentProp = null;
            if (overrideEditor == null && elevateChildAttr == null && !isSimpleType && IsComplexObjectType(elementType))
            {
                elevateAsParentProp = elementType
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.GetProperty)
                    .FirstOrDefault(p => TypeMetadataExtensionRegistrar.HasAttribute<PGElevateAsParentAttribute>(p));
            }

            int index = 0;
            foreach (object element in (System.Collections.IEnumerable)collection)
            {
                int capturedIndex = index;
                var capturedProp = prop;
                var capturedSource = sourceItem;

                string elementEditor = overrideEditor ?? elementType.Name;

                // For IList types, getter/setter work by index
                GetValue elemGetter;
                SetValue elemSetter = null;
                if (collection is System.Collections.IList)
                {
                    elemGetter = () =>
                    {
                        var list = capturedProp.GetValue(capturedSource) as System.Collections.IList;
                        return (list != null && capturedIndex < list.Count) ? list[capturedIndex] : null;
                    };

                    elemSetter = v =>
                    {
                        var list = capturedProp.GetValue(capturedSource) as System.Collections.IList;
                        if (list != null && capturedIndex < list.Count)
                        {
                            list[capturedIndex] = CoerceValueType(v, elementType);
                        }
                    };
                }
                else if (prop.PropertyType.IsArray)
                {
                    elemGetter = () =>
                    {
                        var arr = capturedProp.GetValue(capturedSource) as Array;
                        return (arr != null && capturedIndex < arr.Length) ? arr.GetValue(capturedIndex) : null;
                    };

                    elemSetter = v =>
                    {
                        var arr = capturedProp.GetValue(capturedSource) as Array;
                        if (arr != null && capturedIndex < arr.Length)
                        {
                            arr.SetValue(CoerceValueType(v, elementType), capturedIndex);
                        }
                    };
                }
                else
                {
                    // Non-indexed collection - read-only display of element value
                    object capturedElement = element;
                    elemGetter = () => capturedElement;
                }

                string childPath = $"{prop.Name}[{capturedIndex}]";
                var childTarget = new PropertyEditTarget(childPath, elemGetter, elemSetter)
                {
                    DisplayName = $"[{capturedIndex}]",
                    Editor = elementEditor,
                    EditContext = new PropertyGridListElementContext(listContext, capturedIndex),
                };

                // ------ Apply attribute cascading / elevation ------
                if (overrideEditor != null)
                {
                    // [PGEditor] override: custom editor handles the whole object, no recursion.
                    // Wire up INPC so sub-property edits via the template raise SourceCommitted.
                    if (element is INotifyPropertyChanged)
                    {
                        childTarget.WireUpEditValueINPC(element);
                    }
                }
                else if (elevateChildAttr != null && element != null)
                {
                    // [PGElevateChildProperty("X")] on the list property
                    PropertyInfo childProp = elementType.GetProperty(
                        elevateChildAttr.ChildPropertyName,
                        BindingFlags.Public | BindingFlags.Instance | BindingFlags.GetProperty
                    );

                    if (childProp != null)
                    {
                        _ElevatePropertyForListElement(childTarget, element, elementType, childProp, elemGetter, capturedProp, capturedSource, capturedIndex);
                    }
                }
                else if (elevateAsParentProp != null && element != null)
                {
                    // Element type has [PGElevateAsParent] on one of its properties
                    _ElevatePropertyForListElement(childTarget, element, elementType, elevateAsParentProp, elemGetter, capturedProp, capturedSource, capturedIndex);
                }
                else if (!isSimpleType && element != null && IsComplexObjectType(elementType))
                {
                    // Normal complex type: recurse into sub-properties
                    var subProps = TypeMetadataExtensionRegistrar.GetOrderedProperties(
                        elementType,
                        BindingFlags.Public | BindingFlags.Instance | BindingFlags.GetProperty
                    ).Where(p => p.SetMethod != null || p.GetCustomAttribute<PGShowReadonlyAttribute>() != null);

                    if (subProps.Any())
                    {
                        childTarget.IsExpandable = true;
                        foreach (PropertyEditTarget subChild in GenerateForPropertiesOf(element, depth + 1, maxDepth, limitToTheseProperties))
                        {
                            subChild.Setup();
                            childTarget.InsertChild(childTarget.Children.Count, subChild);
                        }
                    }
                }

                childTarget.Setup();
                listParent.InsertChild(listParent.Children.Count, childTarget);
                ++index;
            }
        }

        /// <summary>
        /// Creates an elevated child target for a list element, making the element row non-expandable
        /// with the elevated property's editor shown inline. Handles struct write-back through the
        /// list when the element type is a value type.
        /// </summary>
        private static void _ElevatePropertyForListElement (
            PropertyEditTarget elementTarget,
            object element,
            Type elementType,
            PropertyInfo elevatedProp,
            GetValue elemGetter,
            PropertyInfo listProp,
            object sourceItem,
            int elementIndex)
        {
            var capturedProp = elevatedProp;
            var capturedElemGetter = elemGetter;
            var capturedListProp = listProp;
            var capturedSource = sourceItem;
            int capturedIndex = elementIndex;

            GetValue elevatedGetter = () =>
            {
                object elem = capturedElemGetter();
                return elem != null ? capturedProp.GetValue(elem) : null;
            };

            SetValue elevatedSetter = null;
            if (capturedProp.SetMethod != null)
            {
                if (elementType.IsValueType)
                {
                    // Struct: read from list, modify property, write back
                    elevatedSetter = v =>
                    {
                        object collection = capturedListProp.GetValue(capturedSource);
                        if (collection is System.Collections.IList list && capturedIndex < list.Count)
                        {
                            object elem = list[capturedIndex];
                            capturedProp.SetValue(elem, CoerceValueType(v, capturedProp.PropertyType));
                            list[capturedIndex] = elem;
                        }
                    };
                }
                else
                {
                    elevatedSetter = v =>
                    {
                        object elem = capturedElemGetter();
                        if (elem != null)
                        {
                            capturedProp.SetValue(elem, CoerceValueType(v, capturedProp.PropertyType));
                        }
                    };
                }
            }

            string elevatedEditor = TypeMetadataExtensionRegistrar.GetAttribute<PGEditorAttribute>(capturedProp)?.Editor
                ?? capturedProp.PropertyType.Name;

            var ctxBuilderAttr = TypeMetadataExtensionRegistrar.GetAttribute<PGEditContextBuilderAttribute>(capturedProp);
            object elevatedEditContext = ctxBuilderAttr != null ? _BuildEditContext(ctxBuilderAttr) : null;

            var elevatedTarget = new PropertyEditTarget(capturedProp.Name, elevatedGetter, elevatedSetter)
            {
                DisplayName = capturedProp.Name.ConvertToFriendlyEn(),
                Editor = elevatedEditor,
                EditContext = elevatedEditContext,
            };

            ApplyDefault(elevatedTarget, capturedProp, element);
            elevatedTarget.Setup();
            elementTarget.ElevatedChildTarget = elevatedTarget;
            elementTarget.IsExpandable = false;

            if (element is INotifyPropertyChanged)
            {
                elementTarget.WireUpElevatedSubObjectINPC(element);
            }
        }

        /// <summary>
        /// Rebuilds the child targets of a list parent after an add/remove/reorder.
        /// Clears existing children and regenerates from the current collection state.
        /// </summary>
        internal static void RebuildListChildren (
            PropertyEditTarget listParent,
            object sourceItem,
            PropertyInfo prop,
            Type elementType,
            PGListAttribute listAttr,
            int depth,
            int maxDepth,
            PropertyMatcher[] limitToTheseProperties)
        {
            // Teardown and remove existing children
            var existingChildren = listParent.Children.OfType<PropertyEditTarget>().ToList();
            foreach (var child in existingChildren)
            {
                child.Teardown();
                listParent.RemoveChild(child);
            }

            // Rebuild
            BuildListChildren(listParent, sourceItem, prop, elementType, listAttr, depth, maxDepth, limitToTheseProperties);

            // Signal that the source has been modified
            listParent.ForceRaiseSourceCommitted();
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

        /// <summary>
        /// When the sub-object behind an elevated child implements INotifyPropertyChanged,
        /// subscribe so that external changes to the elevated property automatically recache
        /// without requiring the top-level source to fire PropertyChanged.
        /// </summary>
        internal void WireUpElevatedSubObjectINPC (object subObject)
        {
            if (m_elevatedSubObject != null)
            {
                m_elevatedSubObject.PropertyChanged -= this.OnElevatedSubObjectPropertyChanged;
            }

            m_elevatedSubObject = subObject as INotifyPropertyChanged;
            if (m_elevatedSubObject != null)
            {
                m_elevatedSubObject.PropertyChanged += this.OnElevatedSubObjectPropertyChanged;
            }
        }

        private void OnElevatedSubObjectPropertyChanged (object sender, PropertyChangedEventArgs e)
        {
            if (m_elevatedChildTarget != null && m_elevatedChildTarget.ShouldEvaluateFor(e.PropertyName))
            {
                m_elevatedChildTarget.RecacheEditValue();
                this.UpdateIsAtDefaultValue();
            }
        }

        /// <summary>
        /// When a [PGEditor] overrides a complex object, the editor template binds into
        /// sub-properties of EditValue directly. Subscribe to the object's INPC so that
        /// sub-property writes raise SourceCommitted and the PropertyGrid knows something changed.
        /// </summary>
        internal void WireUpEditValueINPC (object editValue)
        {
            if (m_editValueINPC != null)
            {
                m_editValueINPC.PropertyChanged -= this.OnEditValueSubPropertyChanged;
            }

            m_editValueINPC = editValue as INotifyPropertyChanged;
            if (m_editValueINPC != null)
            {
                m_editValueINPC.PropertyChanged += this.OnEditValueSubPropertyChanged;
            }
        }

        private void OnEditValueSubPropertyChanged (object sender, PropertyChangedEventArgs e)
        {
            this.RaisePropertyChanged(SourceCommittedPropertyName);
        }

        /// <summary>
        /// Cleans up event subscriptions when this target is being discarded (e.g. during
        /// PropertyGridManager.RebuildEditTargets). Call on every target before clearing the tree.
        /// </summary>
        public void Teardown ()
        {
            if (m_elevatedSubObject != null)
            {
                m_elevatedSubObject.PropertyChanged -= this.OnElevatedSubObjectPropertyChanged;
                m_elevatedSubObject = null;
            }

            if (m_editValueINPC != null)
            {
                m_editValueINPC.PropertyChanged -= this.OnEditValueSubPropertyChanged;
                m_editValueINPC = null;
            }
        }


        // ======================[ SubClasses ]========================================================

        internal struct PropertyMatcher
        {
            private static Regex kIsRegex = new Regex(@"[^\p{L}]");
            private Regex? m_regexMatch;
            private string? m_basicStringSearch; 
            public PropertyMatcher(string propertySearch)
            {
                if (kIsRegex.IsMatch(propertySearch))
                {
                    m_regexMatch = new Regex(propertySearch);
                }
                else
                {
                    m_basicStringSearch = propertySearch;
                }
            }

            public bool IsMatch(PropertyInfo property)
            {
                if (m_regexMatch != null)
                {
                    return m_regexMatch.IsMatch(property.Name);
                }
                return property.Name.Equals(m_basicStringSearch!);
            }
        }

    }
}
