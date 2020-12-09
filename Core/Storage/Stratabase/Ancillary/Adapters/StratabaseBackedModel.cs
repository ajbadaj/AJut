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
    ///         Property<string> m_name;
    ///         public MyModel(Guid id, Stratabase sb) : base(id, sb)
    ///         {
    ///             m_name = this.GenerateProperty<string>(nameof(Name));
    ///         }
    ///         
    ///         public string Name
    ///         {
    ///             get => m_name.Value;
    ///             set => m_name.Access.SetBaselineValue(value);
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

        protected virtual Property<T> GenerateProperty<T> (string propName)
        {
            return new Property<T>(this, propName);
        }

        protected virtual ListProperty<T> GenerateListProperty<T> (string propName)
        {
            return new ListProperty<T>(this, propName);
        }

        protected virtual AdaptedProperty<TStrataValue, TAdaptedValue> GenerateAdaptedProperty<TStrataValue, TAdaptedValue> (string propName, StrataPropertyAdapter<TStrataValue, TAdaptedValue>.ConvertAccessToOutput factory)
        {
            return new AdaptedProperty<TStrataValue, TAdaptedValue>(this, propName, factory);
        }

        protected virtual AdaptedListProperty<TStrataElementValue, TAdaptedElementValue> GenerateAdaptedListProperty<TStrataElementValue, TAdaptedElementValue> (string propName, StrataListPropertyAdapter<TStrataElementValue, TAdaptedElementValue>.ConvertAccessToOutput factory)
        {
            return new AdaptedListProperty<TStrataElementValue, TAdaptedElementValue>(this, propName, factory);
        }

        /// <summary>
        /// A property value backed by a staratabase property value access
        /// </summary>
        public class Property<T> : IDisposable
        {
            private Lazy<T> m_valueCache;
            private readonly StratabaseBackedModel m_owner;
            public Property (StratabaseBackedModel owner, string propertyName)
            {
                m_owner = owner;
                this.Access = owner.SB.GeneratePropertyAccess<T>(owner.Id, propertyName);
                this.Access.ValueChanged += this.Access_OnValueChanged;
                this.ResetCache();
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
            public StrataPropertyValueAccess<T> Access { get; private set; }
            public string Name => this.Access.PropertyName;
            public T Value => m_valueCache.Value;

            protected virtual void OnValueChanged () { }

            private void Access_OnValueChanged (object sender, EventArgs e)
            {
                this.ResetCache();
                m_owner.RaisePropertyChanged(Access.PropertyName);
                this.ValueChanged?.Invoke(this, EventArgs.Empty);
                this.OnValueChanged();
            }

            private void ResetCache ()
            {
                m_valueCache = new Lazy<T>(() => this.Access.GetValue());
            }
        }

        public class ListProperty<TElement> : IDisposable
        {
            private readonly StratabaseBackedModel m_owner;
            public ListProperty (StratabaseBackedModel owner, string propertyName)
            {
                m_owner = owner;
                this.Access = owner.SB.GenerateListPropertyAccess<TElement>(owner.Id, propertyName);
                this.Access.ValueChanged += this.Access_OnValueChanged;
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
            public string Name => this.Access.PropertyName;

            protected virtual void OnValueChanged () { }

            private void Access_OnValueChanged (object sender, EventArgs e)
            {
                m_owner.RaisePropertyChanged(Access.PropertyName);
                this.ValueChanged?.Invoke(this, EventArgs.Empty);
                this.OnValueChanged();
            }
        }

        public class AdaptedProperty<TStrataValue, TAdaptedValue> : IDisposable
        {
            private Property<TStrataValue> m_accessWraper;
            private StrataPropertyAdapter<TStrataValue, TAdaptedValue> m_adapter;

            public AdaptedProperty (StratabaseBackedModel owner, string propertyName, StrataPropertyAdapter<TStrataValue, TAdaptedValue>.ConvertAccessToOutput factory)
            {
                m_accessWraper = new Property<TStrataValue>(owner, propertyName);
                m_adapter = new StrataPropertyAdapter<TStrataValue, TAdaptedValue>(m_accessWraper.Access, factory);
            }

            public string Name => this.Access.PropertyName;
            public TAdaptedValue Value => m_adapter.Value;
            public StrataPropertyValueAccess<TStrataValue> Access => m_accessWraper.Access;

            public void Dispose ()
            {
                m_accessWraper.Dispose();
                m_adapter.Dispose();
            }
        }

        public class AdaptedListProperty<TStrataElementValue, TAdaptedElementValue> : IDisposable
        {
            private ListProperty<TStrataElementValue> m_accessWraper;
            private StrataListPropertyAdapter<TStrataElementValue, TAdaptedElementValue> m_adapter;

            public AdaptedListProperty (StratabaseBackedModel owner, string propertyName, StrataListPropertyAdapter<TStrataElementValue, TAdaptedElementValue>.ConvertAccessToOutput factory)
            {
                m_accessWraper = new ListProperty<TStrataElementValue>(owner, propertyName);
                m_adapter = new StrataListPropertyAdapter<TStrataElementValue, TAdaptedElementValue>(m_accessWraper.Access, factory);
            }

            public string Name => this.Access.PropertyName;
            public ReadOnlyObservableCollection<TAdaptedElementValue> Value => m_adapter.Elements;
            public StrataPropertyListAccess<TStrataElementValue> Access => m_accessWraper.Access;

            public void Dispose ()
            {
                m_accessWraper.Dispose();
                m_adapter.Dispose();
            }
        }
    }
}
