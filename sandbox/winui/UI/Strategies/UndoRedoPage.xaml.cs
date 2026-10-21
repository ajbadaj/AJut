namespace AJutShowRoomWinUI.UI.Strategies
{
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.Runtime.CompilerServices;
    using AJut.UndoRedo;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;

    /// <summary>
    /// Strategy: undo and redo for a small editor. Each edit goes through an UndoRedoManager as a LambdaUndoableAction,
    /// the manager owns the history, and the page only mirrors its state.
    /// </summary>
    public sealed partial class UndoRedoPage : Page, INotifyPropertyChanged
    {
        private const string kStartingTitle = "Untitled";
        private const double kStartingCount = 10.0;

        private readonly UndoRedoManager m_undoRedo = new UndoRedoManager();
        private string m_documentTitle = kStartingTitle;
        private double m_documentCount = kStartingCount;

        public UndoRedoPage ()
        {
            this.InitializeComponent();
            m_undoRedo.PropertyChanged += this.OnUndoRedoPropertyChanged;
            this.Unloaded += this.OnUnloaded;
        }

        // ===========[ Events ]=======================================================
        public event PropertyChangedEventHandler? PropertyChanged;

        // ===========[ Properties ]===================================================

        /// <summary>
        /// The title being edited. Setting it from the UI records an undoable action; the action itself writes the
        /// field through <see cref="ApplyTitle"/>, so undo and redo never re-record.
        /// </summary>
        public string DocumentTitle
        {
            get => m_documentTitle;
            set
            {
                if (m_documentTitle == value)
                {
                    return;
                }

                string before = m_documentTitle;
                string after = value;
                m_undoRedo.ExecuteAction(
                    new LambdaUndoableAction(
                        $"Title: \"{before}\" to \"{after}\"",
                        () => this.ApplyTitle(after),
                        () => this.ApplyTitle(before)
                    )
                );
            }
        }

        /// <summary>
        /// The count being edited. Every nudge of the NumericEditor is one history entry.
        /// </summary>
        public double DocumentCount
        {
            get => m_documentCount;
            set
            {
                if (m_documentCount == value)
                {
                    return;
                }

                double before = m_documentCount;
                double after = value;
                m_undoRedo.ExecuteAction(
                    new LambdaUndoableAction(
                        $"Count: {before} to {after}",
                        () => this.ApplyCount(after),
                        () => this.ApplyCount(before)
                    )
                );
            }
        }

        public bool CanUndo => m_undoRedo.AnyUndos;
        public bool CanRedo => m_undoRedo.AnyRedos;
        public ReadOnlyObservableCollection<IUndoableAction> UndoStack => m_undoRedo.UndoStack;
        public ReadOnlyObservableCollection<IUndoableAction> RedoStack => m_undoRedo.RedoStack;
        public string CurrentStateText => $"Current state: title \"{m_documentTitle}\", count {m_documentCount}";

        public string DemoCode { get; } = @"<!-- The editor: the title commits on focus loss, the count on every nudge -->
<TextBox Header=""Title"" Text=""{x:Bind DocumentTitle, Mode=TwoWay}""/>
<ajut:NumericEditor LabelContent=""Count"" Minimum=""0"" Maximum=""100""
                    Value=""{Binding ElementName=Self, Path=DocumentCount, Mode=TwoWay}""/>
<Button Content=""Undo"" Click=""Undo_OnClick"" IsEnabled=""{x:Bind CanUndo, Mode=OneWay}""/>
<Button Content=""Redo"" Click=""Redo_OnClick"" IsEnabled=""{x:Bind CanRedo, Mode=OneWay}""/>

<!-- The history is the manager's own stacks, newest first -->
<ItemsControl ItemsSource=""{x:Bind UndoStack}"">
    <ItemsControl.ItemTemplate>
        <DataTemplate>
            <TextBlock Text=""{Binding Path=DisplayName}""/>
        </DataTemplate>
    </ItemsControl.ItemTemplate>
</ItemsControl>

// Code behind: one manager, and each edit is a do/undo pair
private readonly UndoRedoManager m_undoRedo = new UndoRedoManager();

public string DocumentTitle
{
    get => m_documentTitle;
    set
    {
        if (m_documentTitle == value) { return; }
        string before = m_documentTitle;
        string after = value;
        m_undoRedo.ExecuteAction(
            new LambdaUndoableAction(
                $""Title: \""{before}\"" to \""{after}\"""",
                () => this.ApplyTitle(after),
                () => this.ApplyTitle(before)
            )
        );
    }
}

// The action writes the field directly, so undo and redo never record themselves
private void ApplyTitle (string title)
{
    m_documentTitle = title;
    this.RaisePropertyChanged(nameof(DocumentTitle));
}

public bool CanUndo => m_undoRedo.AnyUndos;
public bool CanRedo => m_undoRedo.AnyRedos;
public ReadOnlyObservableCollection<IUndoableAction> UndoStack => m_undoRedo.UndoStack;

private void Undo_OnClick (object sender, RoutedEventArgs e) => m_undoRedo.Undo();
private void Redo_OnClick (object sender, RoutedEventArgs e) => m_undoRedo.Redo();

// The manager raises AnyUndos and AnyRedos; the page forwards them to the buttons
private void OnUndoRedoPropertyChanged (object? sender, PropertyChangedEventArgs e)
{
    this.RaisePropertyChanged(nameof(CanUndo));
    this.RaisePropertyChanged(nameof(CanRedo));
}";

        // ===========[ Event handlers ]===============================================
        private void Undo_OnClick (object sender, RoutedEventArgs e) => m_undoRedo.Undo();

        private void Redo_OnClick (object sender, RoutedEventArgs e) => m_undoRedo.Redo();

        private void ClearHistory_OnClick (object sender, RoutedEventArgs e) => m_undoRedo.Clear();

        private void OnUndoRedoPropertyChanged (object? sender, PropertyChangedEventArgs e)
        {
            // The manager only raises the one that changed, and Redo raises at most one of the two, so refresh both
            this.RaisePropertyChanged(nameof(CanUndo));
            this.RaisePropertyChanged(nameof(CanRedo));
        }

        private void OnUnloaded (object sender, RoutedEventArgs e)
        {
            this.Unloaded -= this.OnUnloaded;
            m_undoRedo.PropertyChanged -= this.OnUndoRedoPropertyChanged;
        }

        // ===========[ Helpers ]======================================================
        private void ApplyTitle (string title)
        {
            m_documentTitle = title;
            this.RaisePropertyChanged(nameof(DocumentTitle));
            this.RaisePropertyChanged(nameof(CurrentStateText));
        }

        private void ApplyCount (double count)
        {
            m_documentCount = count;
            this.RaisePropertyChanged(nameof(DocumentCount));
            this.RaisePropertyChanged(nameof(CurrentStateText));
        }

        private void RaisePropertyChanged ([CallerMemberName] string propertyName = "")
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
