namespace AJut.Storage
{
    using System;
    using System.Collections.ObjectModel;

    /// <summary>
    /// A base class for a <see cref="Stratabase"/> backed model, makes easier access to forward value change events via the <see cref="System.ComponentModel.INotifyPropertyChanged.PropertyChanged"/> event.
    /// Flyweight <see cref="Stratabase"/> access instances can be generated and used via the overridable Generate functions.
    /// </summary>
    /// <example>
    ///     <code>
    ///     class MyModel : StratabaseBackedModel
    ///     {
    ///         Property&lt;string&gt; m_name;
    ///         public MyModel(Guid id, Stratabase sb) : base(id, sb)
    ///         {
    ///             m_name = this.GenerateProperty&lt;string&gt;(nameof(Name));
    ///         }
    ///         
    ///         public string Name
    ///         {
    ///             get =&gt; m_name.Value;
    ///             set =&gt; m_name.Access.SetBaselineValue(value);
    ///         }
    ///     }
    ///     </code>
    /// </example>
    public abstract class StratabaseBackedModel : NotifyPropertyChanged
    {
        public StratabaseBackedModel (Guid id, Stratabase sb)
        {
            this.Id = id;
            this.SB = sb;
        }

        public Guid Id { get; }
        public Stratabase SB { get; }

        protected virtual Property<T> GenerateProperty<T> (string propName, T defaultValue = default)
        {
            return new Property<T>(this, GeneratePropertyPath(propName), defaultValue);
        }

        protected virtual ListProperty<T> GenerateListProperty<T> (string propName)
        {
            return new ListProperty<T>(this, GeneratePropertyPath(propName));
        }

        protected virtual AdaptedProperty<TStrataValue, TAdaptedValue> GenerateAdaptedProperty<TStrataValue, TAdaptedValue> (string propName, StrataPropertyAdapter<TStrataValue, TAdaptedValue>.ConvertAccessToOutput factory, TStrataValue defaultValue = default)
        {
            return new AdaptedProperty<TStrataValue, TAdaptedValue>(this, GeneratePropertyPath(propName), factory, defaultValue);
        }

        protected virtual AdaptedListProperty<TStrataElementValue, TAdaptedElementValue> GenerateAdaptedListProperty<TStrataElementValue, TAdaptedElementValue> (string propName, StrataListPropertyAdapter<TStrataElementValue, TAdaptedElementValue>.ConvertAccessToOutput factory)
        {
            return new AdaptedListProperty<TStrataElementValue, TAdaptedElementValue>(this, GeneratePropertyPath(propName), factory);
        }

        protected virtual string GeneratePropertyPath (string propName) => propName;
        protected virtual void OnNewPropertyGenerated (IStrataBackedModelProperty prop) { }

        // =======================================[ Child Classes ]===========================================

        /// <summary>
        /// A property value backed by a staratabase property value access
        /// </summary>
        public class Property<T> : IStrataBackedModelProperty
        {
            private Lazy<T> m_valueCache;
            private readonly StratabaseBackedModel m_owner;
            public Property (StratabaseBackedModel owner, string propertyName, T defaultValue)
            {
                m_owner = owner;
                this.Access = m_owner.SB.GeneratePropertyAccess<T>(m_owner.Id, propertyName);
                this.Access.ValueChanged += this.Access_OnValueChanged;
                this.ResetCache();
                if (!this.Access.IsBaselineSet)
                {
                    this.Access.SetBaselineValue(defaultValue);
                }

                m_owner.OnNewPropertyGenerated(this);
            }

            public void Dispose ()
            {
                if (this.Access != null)
                {
                    this.Access.ValueChanged -= this.Access_OnValueChanged;
                    this.Access.Dispose();
                    this.Access = null;
                }

                this.HandleDisposeCachedValue();
            }

            public event EventHandler<EventArgs> ValueChanged;
            public StrataPropertyValueAccess<T> Access { get; private set; }
            IStrataPropertyAccess IStrataBackedModelProperty.Access => this.Access;
            public string Name => this.Access.PropertyName;
            public T Value => m_valueCache.Value;

            protected virtual void OnValueChanged () { }

            private void Access_OnValueChanged (object sender, EventArgs e)
            {
                this.ResetCache();
                m_owner.RaisePropertyChanged(this.Name);
                this.ValueChanged?.Invoke(this, EventArgs.Empty);
                this.OnValueChanged();
            }

            private void HandleDisposeCachedValue ()
            {
                if (m_valueCache?.IsValueCreated == true && m_valueCache.Value is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            private void ResetCache ()
            {
                this.HandleDisposeCachedValue();
                m_valueCache = new Lazy<T>(() => this.Access.GetValue());
            }
        }

        /// <summary>
        /// A list property value backed by a stratabase <see cref="StrataPropertyListAccess{TElement}"/> value access
        /// </summary>
        public class ListProperty<TElement> : IStrataBackedModelProperty
        {
            private readonly StratabaseBackedModel m_owner;
            public ListProperty (StratabaseBackedModel owner, string propertyName)
            {
                m_owner = owner;
                this.Access = owner.SB.GenerateListPropertyAccess<TElement>(owner.Id, propertyName);
                this.Access.ValueChanged += this.Access_OnValueChanged;
                m_owner.OnNewPropertyGenerated(this);
            }

            public void Dispose ()
            {
                if (this.Access != null)
                {
                    this.Access.ValueChanged -= this.Access_OnValueChanged;
                    this.Access.Dispose();
                    this.Access = null;
                }
            }

            public event EventHandler<EventArgs> ValueChanged;
            public StrataPropertyListAccess<TElement> Access { get; private set; }
            IStrataPropertyAccess IStrataBackedModelProperty.Access => this.Access;
            public string Name => this.Access.PropertyName;

            protected virtual void OnValueChanged () { }

            private void Access_OnValueChanged (object sender, EventArgs e)
            {
                m_owner.RaisePropertyChanged(Access.PropertyName);
                this.ValueChanged?.Invoke(this, EventArgs.Empty);
                this.OnValueChanged();
            }
        }

        public class AdaptedProperty<TStrataValue, TAdaptedValue> : IStrataBackedModelProperty
        {
            private readonly Property<TStrataValue> m_accessWraper;
            private readonly StrataPropertyAdapter<TStrataValue, TAdaptedValue> m_adapter;
            private readonly StratabaseBackedModel m_owner;

            public AdaptedProperty (StratabaseBackedModel owner, string propertyName, StrataPropertyAdapter<TStrataValue, TAdaptedValue>.ConvertAccessToOutput factory, TStrataValue defaultValue)
            {
                m_owner = owner;
                
                m_accessWraper = new Property<TStrataValue>(owner, propertyName, defaultValue);
                m_adapter = new StrataPropertyAdapter<TStrataValue, TAdaptedValue>(m_accessWraper.Access, factory);
                m_adapter.ValueChanged += this.OnAdapterValueReset;
                
                m_owner.OnNewPropertyGenerated(this);
            }

            public AdaptedProperty (StratabaseBackedModel owner, string propertyName, TStrataValue defaultValue, StrataPropertyAdapter<TStrataValue, TAdaptedValue>.ConvertAccessToOutput factory)
            {
                m_owner = owner;
                
                m_accessWraper = new Property<TStrataValue>(owner, propertyName, defaultValue);
                m_adapter = new StrataPropertyAdapter<TStrataValue, TAdaptedValue>(m_accessWraper.Access, factory);
                m_adapter.ValueChanged += this.OnAdapterValueReset;
            }

            public event EventHandler<EventArgs> ValueChanged;

            public void Dispose ()
            {
                m_adapter.ValueChanged -= this.OnAdapterValueReset;
                m_adapter.Dispose();
            }

            private void OnAdapterValueReset (object sender, EventArgs e)
            {
                m_owner.RaisePropertyChanged(this.Name);
                this.ValueChanged?.Invoke(this, EventArgs.Empty);
            }

            public string Name => this.Access.PropertyName;
            public TAdaptedValue Value => m_adapter.Value;
            public StrataPropertyValueAccess<TStrataValue> Access => m_accessWraper.Access;
            IStrataPropertyAccess IStrataBackedModelProperty.Access => this.Access;
        }

        public class AdaptedListProperty<TStrataElementValue, TAdaptedElementValue> : IStrataBackedModelProperty
        {
            private readonly StratabaseBackedModel m_owner;
            private ListProperty<TStrataElementValue> m_accessWraper;
            private StrataListPropertyAdapter<TStrataElementValue, TAdaptedElementValue> m_adapter;

            public AdaptedListProperty (StratabaseBackedModel owner, string propertyName, StrataListPropertyAdapter<TStrataElementValue, TAdaptedElementValue>.ConvertAccessToOutput factory)
            {
                m_owner = owner;
                m_accessWraper = new ListProperty<TStrataElementValue>(owner, propertyName);
                m_adapter = new StrataListPropertyAdapter<TStrataElementValue, TAdaptedElementValue>(m_accessWraper.Access, factory);

                m_owner.OnNewPropertyGenerated(this);
            }

            public event EventHandler<EventArgs> ValueChanged;

            public string Name => this.Access.PropertyName;
            public ReadOnlyObservableCollection<TAdaptedElementValue> Value => m_adapter.Elements;
            public StrataPropertyListAccess<TStrataElementValue> Access => m_accessWraper.Access;
            IStrataPropertyAccess IStrataBackedModelProperty.Access => this.Access;

            protected virtual void OnValueChanged () { }

            private void Access_OnValueChanged (object sender, EventArgs e)
            {
                m_owner.RaisePropertyChanged(Access.PropertyName);
                this.ValueChanged?.Invoke(this, EventArgs.Empty);
                this.OnValueChanged();
            }

            public void Dispose ()
            {
                m_adapter.Dispose();
            }
        }
    }
}
