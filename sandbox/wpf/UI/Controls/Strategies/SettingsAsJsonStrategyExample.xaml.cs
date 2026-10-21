namespace TheAJutShowRoom.UI.Controls.Strategies
{
    using System.Windows;
    using System.Windows.Controls;
    using AJut;
    using AJut.Text.AJson;
    using DPUtils = AJut.UX.DPUtils<SettingsAsJsonStrategyExample>;

    public partial class SettingsAsJsonStrategyExample : UserControl
    {
        public SettingsAsJsonStrategyExample ()
        {
            this.Settings = new DemoSettings();
            this.InitializeComponent();
            this.Save_OnClick(this, new RoutedEventArgs());
        }

        public static readonly DependencyProperty SettingsProperty = DPUtils.Register(_ => _.Settings);
        public DemoSettings Settings
        {
            get => (DemoSettings)this.GetValue(SettingsProperty);
            set => this.SetValue(SettingsProperty, value);
        }

        public static readonly DependencyProperty JsonTextProperty = DPUtils.Register(_ => _.JsonText, string.Empty);
        public string JsonText
        {
            get => (string)this.GetValue(JsonTextProperty);
            set => this.SetValue(JsonTextProperty, value);
        }

        public static readonly DependencyProperty StatusTextProperty = DPUtils.Register(_ => _.StatusText, string.Empty);
        public string StatusText
        {
            get => (string)this.GetValue(StatusTextProperty);
            set => this.SetValue(StatusTextProperty, value);
        }

        public string CodeText { get; } =
@"// Plain properties. NotifyPropertyChanged is here for the bindings, not for the JSON.
public class DemoSettings : NotifyPropertyChanged
{
    public string DisplayName { get; set; } = ""New user"";
    public float Volume { get; set; } = 65.0f;
    public bool AutoSave { get; set; } = true;
    public eStartupPage StartupPage { get; set; } = eStartupPage.Home;
}

// Save: object to Json, Json to text
private void Save_OnClick (object sender, RoutedEventArgs e)
{
    Json json = JsonHelper.BuildJsonForObject(this.Settings);
    if (!json)
    {
        this.StatusText = $""Could not build JSON: {json.GetErrorReport()}"";
        return;
    }

    this.JsonText = json.ToString();
}

// Load: text to Json, Json to a new object
private void Load_OnClick (object sender, RoutedEventArgs e)
{
    Json json = JsonHelper.ParseText(this.JsonText);
    if (!json)
    {
        this.StatusText = $""Could not parse JSON: {json.GetErrorReport()}"";
        return;
    }

    DemoSettings? loaded = JsonHelper.BuildObjectForJson<DemoSettings>(json);
    if (loaded == null)
    {
        this.StatusText = ""JSON parsed, but did not describe a DemoSettings."";
        return;
    }

    this.Settings = loaded;
}

// On disk it is the same two calls around File.ReadAllText / File.WriteAllText, and
// ApplicationUtilities.BuildAppDataProjectPath(""settings.json"") gives a per-app location.";

        private void Save_OnClick (object sender, RoutedEventArgs e)
        {
            Json json = JsonHelper.BuildJsonForObject(this.Settings);
            if (!json)
            {
                this.StatusText = $"Could not build JSON: {json.GetErrorReport()}";
                return;
            }

            this.JsonText = json.ToString();
            this.StatusText = "Saved the settings into the JSON box.";
        }

        private void Load_OnClick (object sender, RoutedEventArgs e)
        {
            Json json = JsonHelper.ParseText(this.JsonText);
            if (!json)
            {
                this.StatusText = $"Could not parse JSON: {json.GetErrorReport()}";
                return;
            }

            DemoSettings? loaded = JsonHelper.BuildObjectForJson<DemoSettings>(json);
            if (loaded == null)
            {
                this.StatusText = "JSON parsed, but did not describe a DemoSettings.";
                return;
            }

            this.Settings = loaded;
            this.StatusText = "Loaded the JSON box into the settings.";
        }

        public enum eStartupPage
        {
            Home,
            LastOpened,
            Controls,
        }

        /// <summary>
        /// The settings being saved. Nothing here is annotated for JSON: the helper reads public properties by reflection.
        /// </summary>
        public class DemoSettings : NotifyPropertyChanged
        {
            private string m_displayName = "New user";
            public string DisplayName
            {
                get => m_displayName;
                set => this.SetAndRaiseIfChanged(ref m_displayName, value);
            }

            private float m_volume = 65.0f;
            public float Volume
            {
                get => m_volume;
                set => this.SetAndRaiseIfChanged(ref m_volume, value);
            }

            private bool m_autoSave = true;
            public bool AutoSave
            {
                get => m_autoSave;
                set => this.SetAndRaiseIfChanged(ref m_autoSave, value);
            }

            private eStartupPage m_startupPage = eStartupPage.Home;
            public eStartupPage StartupPage
            {
                get => m_startupPage;
                set => this.SetAndRaiseIfChanged(ref m_startupPage, value);
            }
        }
    }
}
