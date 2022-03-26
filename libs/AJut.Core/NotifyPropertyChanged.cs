namespace AJut
{
    using System.ComponentModel;
    using System.Linq;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// A convenience base class that implements <see cref="INotifyPropertyChanged"/> and provides useful functionality to interfacing with that and building properties in a more streamlined fashion.
    /// </summary>
    public abstract class NotifyPropertyChanged : INotifyPropertyChanged
    {
        /// <summary>
        /// Occurs when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Raise the <see cref="PropertyChanged"/> event, passing in the entire <see cref="PropertyChangedEventArgs"/> rather than just a property name.
        /// </summary>
        protected void RaisePropertyChanged (PropertyChangedEventArgs propertyChangedEventArgs)
        {
            this.PropertyChanged?.Invoke(this, propertyChangedEventArgs);
        }

        /// <summary>
        /// Raises the <see cref="PropertyChanged"/> event for each property passed in
        /// </summary>
        protected void RaisePropertiesChanged (params string[] propertyNames)
        {
            foreach (string propertyName in propertyNames)
            {
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        /// <summary>
        /// Raise the <see cref="PropertyChanged"/> event for the property name
        /// </summary>
        /// <param name="propertyName">The property name - this will default to determination from the <see cref="CallerMemberNameAttribute"/>. NOTE: This breaks if ANYTHING passed to this function utilizes dynamic</param>
        /// <remarks>
        /// BEWARE: Unfortunately, the automatic population of <paramref name="propertyName"/> will fail and utilize the default (empty string) if ANY parameter passed in is the result of a dynamic function.
        /// Per microsoft, this is not a bug but an explicit design decision: https://developercommunity.visualstudio.com/t/callermembername-attribute-is-broken-in-net-5/
        /// </remarks>
        protected void RaisePropertyChanged ([CallerMemberName] string propertyName = "")
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// The main evaluator for if two values match, this is used by the <see cref="SetAndRaiseIfChanged{T}"/> functions.
        /// </summary>
        protected virtual bool DoValuesMatch<T> (ref T value, T newValue)
        {
            return (value == null && newValue == null) || (value?.Equals(newValue) ?? false);
        }

        /// <summary>
        /// Set a property's backing value, and raise the property changed for that value if a change has occured
        /// </summary>
        /// <param name="value">A ref to the backing field</param>
        /// <param name="newValue">The new value</param>
        /// <param name="propertyName">The property name - this will default to determination from the <see cref="CallerMemberNameAttribute"/>. NOTE: This breaks if ANYTHING passed to this function utilizes dynamic</param>
        /// <returns>True if the value changed, false otherwise</returns>
        /// <remarks>
        /// BEWARE: Unfortunately, the automatic population of <paramref name="propertyName"/> will fail and utilize the default (empty string) if ANY parameter passed in is the result of a dynamic function.
        /// Per microsoft, this is not a bug but an explicit design decision: https://developercommunity.visualstudio.com/t/callermembername-attribute-is-broken-in-net-5/
        /// </remarks>
        protected bool SetAndRaiseIfChanged<T> (ref T value, T newValue, [CallerMemberName] string propertyName = "")
        {
            if (this.DoValuesMatch(ref value, newValue))
            {
                return false;
            }

            value = newValue;
            this.RaisePropertyChanged(propertyName);
            return true;
        }

        /// <summary>
        /// Set a property's backing value, and raise the property changed for that value if a change has occured
        /// </summary>
        /// <param name="value">A ref to the backing field</param>
        /// <param name="newValue">The new value</param>
        /// <param name="propertyName">The name of the property being changed</param>
        /// <param name="andRaise">Names of other properties which we should also raise "property changed" for should this property be successfully changed.</param>
        protected bool SetAndRaiseIfChanged<T> (ref T value, T newValue, string propertyName, params string[] andRaise)
        {
            if (this.DoValuesMatch(ref value, newValue))
            {
                return false;
            }

            value = newValue;
            this.RaisePropertyChanged(propertyName);
            if (andRaise?.Any() ?? false)
            {
                this.RaisePropertiesChanged(andRaise);
            }

            return true;
        }
    }
}
