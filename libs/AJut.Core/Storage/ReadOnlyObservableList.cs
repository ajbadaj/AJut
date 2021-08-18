namespace AJut.Storage
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using AJut.Storage.ListOperations;

    public class ReadOnlyObservableList<T> : ObservableListBase<T>, IDisposable
    {
        // =================================[ Fields ]=================================
        private ObservableListBase<T> m_backing;

        // ==========================[ Construction/ Destruction ]====================
        public ReadOnlyObservableList (ObservableListBase<T> source) : base(source)
        {
            m_backing = source;
            m_backing.ListCompositionChanged += this.OnBackingListCompositionChanged;
            m_backing.PropertyChanged += this.OnBackingPropertyChanged;
        }

        public void Dispose ()
        {
            m_backing.PropertyChanged -= this.OnBackingPropertyChanged;
            m_backing.ListCompositionChanged -= this.OnBackingListCompositionChanged;
            m_backing = null;
        }

        // =========================== [ Properties ]=================================
        public override bool IsReadOnly => true;

        // =========================== [ Interface Methods ]==========================

        // =========================== [ Overridable Interface ] ======================

        protected override int DoInsert (int index, IEnumerable<T> elements)
        {
            throw GenerateException("add element");
        }

        protected override int DoRemoveEach (IEnumerable<T> elements)
        {
            throw GenerateException("remove element");
        }

        protected override void DoReplace (int index, T value)
        {
            throw GenerateException("replace element");
        }

        protected override void DoClear ()
        {
            throw GenerateException("clear all elements");
        }

        protected override void DoReverse (int startIndex, int count)
        {
            throw GenerateException("in-place reverse elements");
        }

        protected override void DoSwap (int leftIndex, int rightIndex)
        {
            throw GenerateException("perform an element swap");
        }

        // =================================[ Private Methods ]==================================

        private void OnBackingListCompositionChanged (object sender, NotifyListCompositionChangedEventArgs<T> e)
        {
            this.RaiseAllStandardEvents(e);
        }

        private void OnBackingPropertyChanged (object sender, PropertyChangedEventArgs e)
        {
            this.RaisePropertyChanged(e);
        }

        private static Exception GenerateException(string whatCantYouDo) => new InvalidOperationException($"You cannot {whatCantYouDo}, this list is read-only");
    }
}
