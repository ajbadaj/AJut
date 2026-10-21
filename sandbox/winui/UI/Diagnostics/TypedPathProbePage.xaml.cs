namespace AJutShowRoomWinUI.UI.Diagnostics
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;

    /// <summary>
    /// Pushes text into a PathSelectionControl's inner TextBox, which is the same route a keystroke takes, and
    /// checks the control revalidates afterward. If a typed edit moves SelectedPath without rerunning validation
    /// then the missing-folder border can only ever come from a browsed path, and browsing cannot produce a path
    /// that is missing, which reads from the outside like the warning was never implemented.
    /// </summary>
    public sealed partial class TypedPathProbePage : Page
    {
        private const string kProbeMissingFolderPath = @"C:\_ajut_showroom_probe\definitely_not_here";

        public TypedPathProbePage ()
        {
            this.InitializeComponent();
        }

        private async void PathProbe_OnTypedPathClicked (object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null) { button.IsEnabled = false; }
            try
            {
                this.PathProbe_Output.Text = string.Empty;

                TextBox? pathTextBox = LeakProbeHelpers.CollectDescendants<TextBox>(this.PathProbe_Control)
                                            .FirstOrDefault(t => t.Name == "PART_PathTextBox");
                if (pathTextBox == null)
                {
                    this.PathProbe_AppendLine("INCONCLUSIVE - PART_PathTextBox never realized (template not applied yet?)");
                    return;
                }

                var results = new List<string>();
                bool allPass = true;

                // Baseline: empty it out so each step below is a genuine edit.
                pathTextBox.Text = string.Empty;
                await PathProbe_SettleAsync();

                // 1. Type a folder that does not exist - has to go invalid, with a reason.
                pathTextBox.Text = kProbeMissingFolderPath;
                await PathProbe_SettleAsync();
                allPass &= this.PathProbe_Check(
                    results,
                    "Typed a missing folder",
                    expectValid: false,
                    expectExists: false,
                    expectedPath: kProbeMissingFolderPath
                );

                // 2. Type one that does exist - has to come back valid. Deliberately not the app's
                // own base directory: under a packaged run that lands in AppX, where the path is
                // virtualized and Directory.Exists can answer false for a folder that is plainly there.
                string existingFolder = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                pathTextBox.Text = existingFolder;
                await PathProbe_SettleAsync();
                allPass &= this.PathProbe_Check(
                    results,
                    "Typed an existing folder",
                    expectValid: true,
                    expectExists: true,
                    expectedPath: existingFolder
                );

                this.PathProbe_AppendLine(allPass
                    ? "PASS - typed paths revalidate"
                    : "FAIL - a typed edit moved SelectedPath without rerunning validation");
                foreach (string line in results)
                {
                    this.PathProbe_AppendLine("    " + line);
                }
            }
            finally
            {
                if (button != null) { button.IsEnabled = true; }
            }
        }

        private bool PathProbe_Check (List<string> results, string label, bool expectValid, bool expectExists, string expectedPath)
        {
            bool pathOk = string.Equals(this.PathProbe_Control.SelectedPath, expectedPath, StringComparison.Ordinal);
            bool validOk = this.PathProbe_Control.IsPathValid == expectValid;
            bool existsOk = this.PathProbe_Control.DoesPathExist == expectExists;
            bool reasonOk = expectValid
                ? string.IsNullOrEmpty(this.PathProbe_Control.InvalidPathReason)
                : !string.IsNullOrEmpty(this.PathProbe_Control.InvalidPathReason);

            bool ok = pathOk && validOk && existsOk && reasonOk;
            results.Add(string.Format(
                "{0}: {1} | SelectedPath={2} (want {3}), IsPathValid={4} (want {5}), DoesPathExist={6} (want {7}), reason='{8}'",
                label,
                ok ? "ok" : "MISMATCH",
                this.PathProbe_Control.SelectedPath ?? "(null)",
                expectedPath,
                this.PathProbe_Control.IsPathValid,
                expectValid,
                this.PathProbe_Control.DoesPathExist,
                expectExists,
                this.PathProbe_Control.InvalidPathReason ?? string.Empty
            ));
            return ok;
        }

        // TextBox.TextChanged does not always come back on the same tick as the Text assignment,
        // and the control defers a caret move of its own, so drain before reading the result.
        private static async Task PathProbe_SettleAsync ()
        {
            for (int i = 0; i < 3; ++i)
            {
                await LeakProbeHelpers.DrainDispatcherAsync();
                await Task.Delay(60);
            }
        }

        private void PathProbe_AppendLine (string line)
        {
            this.PathProbe_Output.Text = this.PathProbe_Output.Text.Length == 0
                ? line
                : this.PathProbe_Output.Text + "\n" + line;
        }
    }
}
