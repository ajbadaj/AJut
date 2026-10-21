namespace TheAJutShowRoom.UI.Controls.Strategies
{
    using System.Windows;
    using System.Windows.Controls;
    using AJut;
    using AJut.UndoRedo;
    using AJut.UX;

    public partial class UndoRedoStrategyExample : UserControl
    {
        public UndoRedoStrategyExample ()
        {
            this.UndoRedo = new UndoRedoManager();
            this.Document = new EditableDocument { Name = "Untitled", Number = 1.0f };
            this.InitializeComponent();
        }

        public UndoRedoManager UndoRedo { get; }
        public EditableDocument Document { get; }

        public string CodeText { get; } =
@"// One manager per document (or per editor); it is a NotifyPropertyChanged so AnyUndos and AnyRedos bind
public UndoRedoManager UndoRedo { get; } = new UndoRedoManager();

// An edit the manager performs: ExecuteAction runs Do() and then records the action
private void ApplyName_OnClick (object sender, RoutedEventArgs e)
{
    string oldName = this.Document.Name;
    string newName = this.NameInput.Text;
    if (oldName == newName)
    {
        return;
    }

    this.UndoRedo.ExecuteAction(new LambdaUndoableAction(
        $""Rename to '{newName}'"",
        () => this.Document.Name = newName,
        () => this.Document.Name = oldName
    ));
}

// An edit that already happened (the NumericEditor applied it through its binding): AddAction only records it
private void Number_OnUserEditComplete (object sender, UserEditAppliedEventArgs e)
{
    if (e.OldValue is not float oldNumber || e.NewValue is not float newNumber || oldNumber == newNumber)
    {
        return;
    }

    this.UndoRedo.AddAction(new LambdaUndoableAction(
        $""Number {oldNumber} to {newNumber}"",
        () => this.Document.Number = newNumber,
        () => this.Document.Number = oldNumber
    ));
}

private void Undo_OnClick (object sender, RoutedEventArgs e) => this.UndoRedo.Undo();
private void Redo_OnClick (object sender, RoutedEventArgs e) => this.UndoRedo.Redo();

// The history lists are the manager's own read-only observable collections
//   <ListBox ItemsSource=""{Binding Path=UndoRedo.UndoStack}"" DisplayMemberPath=""DisplayName""/>
//   <Button Content=""Undo"" IsEnabled=""{Binding Path=UndoRedo.AnyUndos}""/>";

        private void ApplyName_OnClick (object sender, RoutedEventArgs e)
        {
            string oldName = this.Document.Name;
            string newName = this.NameInput.Text;
            if (oldName == newName)
            {
                return;
            }

            this.UndoRedo.ExecuteAction(new LambdaUndoableAction(
                $"Rename to '{newName}'",
                () => this.Document.Name = newName,
                () => this.Document.Name = oldName
            ));
        }

        private void Number_OnUserEditComplete (object sender, UserEditAppliedEventArgs e)
        {
            if (e.OldValue is not float oldNumber || e.NewValue is not float newNumber || oldNumber == newNumber)
            {
                return;
            }

            this.UndoRedo.AddAction(new LambdaUndoableAction(
                $"Number {oldNumber} to {newNumber}",
                () => this.Document.Number = newNumber,
                () => this.Document.Number = oldNumber
            ));
        }

        private void Undo_OnClick (object sender, RoutedEventArgs e)
        {
            this.UndoRedo.Undo();
        }

        private void Redo_OnClick (object sender, RoutedEventArgs e)
        {
            this.UndoRedo.Redo();
        }

        private void ClearHistory_OnClick (object sender, RoutedEventArgs e)
        {
            this.UndoRedo.Clear();
        }

        /// <summary>
        /// The thing being edited. Plain observable properties; the undo manager wraps the edits, not the object.
        /// </summary>
        public class EditableDocument : NotifyPropertyChanged
        {
            private string m_name = string.Empty;
            public string Name
            {
                get => m_name;
                set => this.SetAndRaiseIfChanged(ref m_name, value);
            }

            private float m_number;
            public float Number
            {
                get => m_number;
                set => this.SetAndRaiseIfChanged(ref m_number, value);
            }
        }
    }
}
