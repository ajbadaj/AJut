namespace AJut
{
    using System.ComponentModel;
    using System.Linq;
    using System.Runtime.CompilerServices;

    public abstract class NotifyPropertyChanged : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void RaisePropertiesChanged(params string[] propertyNames)
        {
            foreach (string propertyName in propertyNames)
            {
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        protected void RaisePropertyChanged([CallerMemberName]string propertyName = "")
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool DoValuesMatch<T> (ref T value, T newValue)
        {
            return (value == null && newValue == null) || (value?.Equals(newValue) ?? false);
        }

        protected bool SetAndRaiseIfChanged<T> (ref T value, T newValue, [CallerMemberName]string propertyName = "")
        {
            if (this.DoValuesMatch(ref value, newValue))
            {
                return false;
            }

            value = newValue;
            this.RaisePropertyChanged(propertyName);
            return true;
        }

        protected bool SetAndRaiseIfChanged<T> (ref T value, T newValue, [CallerMemberName] string propertyName = "", params string[] andRaise)
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
