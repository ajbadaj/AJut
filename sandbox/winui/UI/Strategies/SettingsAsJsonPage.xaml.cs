namespace AJutShowRoomWinUI.UI.Strategies
{
    using System.ComponentModel;
    using System.Runtime.CompilerServices;
    using AJut.Text.AJson;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;

    /// <summary>
    /// Strategy: a settings class with plain properties, written to JSON text and read back with JsonHelper. The
    /// text box in the middle stands in for the file.
    /// </summary>
    public sealed partial class SettingsAsJsonPage : Page, INotifyPropertyChanged
    {
        private ShowroomSettings m_settings = new ShowroomSettings();
        private string m_jsonText = string.Empty;
        private string m_statusText = string.Empty;

        public SettingsAsJsonPage ()
        {
            this.InitializeComponent();
            this.SaveSettingsToJsonText();
        }

        // ===========[ Events ]=======================================================
        public event PropertyChangedEventHandler? PropertyChanged;

        // ===========[ Properties ]===================================================

        /// <summary>
        /// The settings being edited. Load replaces the whole object, and the bindings re-read it from here.
        /// </summary>
        public ShowroomSettings Settings
        {
            get => m_settings;
            private set
            {
                m_settings = value;
                this.RaisePropertyChanged();
            }
        }

        public string JsonText
        {
            get => m_jsonText;
            set
            {
                m_jsonText = value;
                this.RaisePropertyChanged();
            }
        }

        public string StatusText
        {
            get => m_statusText;
            private set
            {
                m_statusText = value;
                this.RaisePropertyChanged();
            }
        }

        public string DemoCode { get; } = @"// A settings class is just properties. No attributes, no base class, no serializer.
public class ShowroomSettings
{
    public string UserName { get; set; } = ""Nobody"";
    public int FontSize { get; set; } = 14;
    public bool UseDarkMode { get; set; } = true;
    public string LastOpenedFolder { get; set; } = string.Empty;
}

// Save: build the json and check it before trusting the text
Json json = JsonHelper.BuildJsonForObject(this.Settings);
if (json.HasErrors)
{
    this.StatusText = json.GetErrorReport();
    return;
}

File.WriteAllText(settingsPath, json.ToString());

// Load: parse the text, then build the object from it
Json parsed = JsonHelper.ParseText(File.ReadAllText(settingsPath));
if (parsed.HasErrors)
{
    this.StatusText = parsed.GetErrorReport();
    return;
}

ShowroomSettings? loaded = JsonHelper.BuildObjectForJson<ShowroomSettings>(parsed);
if (loaded != null)
{
    this.Settings = loaded;
}

<!-- The fields bind straight to the object; replacing it re-reads every one -->
<TextBox Header=""User name"" Text=""{x:Bind Settings.UserName, Mode=TwoWay}""/>
<ajut:NumericEditor LabelContent=""Font size"" Minimum=""6"" Maximum=""72""
                    Value=""{Binding ElementName=Self, Path=Settings.FontSize, Mode=TwoWay}""/>
<ToggleSwitch Header=""Dark mode"" IsOn=""{x:Bind Settings.UseDarkMode, Mode=TwoWay}""/>";

        // ===========[ Event handlers ]===============================================
        private void Save_OnClick (object sender, RoutedEventArgs e)
        {
            this.SaveSettingsToJsonText();
        }

        private void Load_OnClick (object sender, RoutedEventArgs e)
        {
            Json parsed = JsonHelper.ParseText(this.JsonText);
            if (parsed.HasErrors)
            {
                this.StatusText = "Load failed: " + parsed.GetErrorReport();
                return;
            }

            ShowroomSettings? loaded = JsonHelper.BuildObjectForJson<ShowroomSettings>(parsed);
            if (loaded == null)
            {
                this.StatusText = "Load failed: the JSON parsed, but did not describe a ShowroomSettings";
                return;
            }

            this.Settings = loaded;
            this.StatusText = $"Loaded: user name \"{loaded.UserName}\", font size {loaded.FontSize}, dark mode {loaded.UseDarkMode}";
        }

        // ===========[ Helpers ]======================================================
        private void SaveSettingsToJsonText ()
        {
            Json json = JsonHelper.BuildJsonForObject(this.Settings);
            if (json.HasErrors)
            {
                this.StatusText = "Save failed: " + json.GetErrorReport();
                return;
            }

            this.JsonText = json.ToString();
            this.StatusText = "Saved the settings object to the JSON on the right";
        }

        private void RaisePropertyChanged ([CallerMemberName] string propertyName = "")
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // ===========[ Subclasses ]===================================================

        /// <summary>
        /// The settings class the demo saves and loads. Plain properties with defaults are all AJson needs.
        /// </summary>
        public class ShowroomSettings
        {
            public string UserName { get; set; } = "Nobody";
            public int FontSize { get; set; } = 14;
            public bool UseDarkMode { get; set; } = true;
            public string LastOpenedFolder { get; set; } = string.Empty;
        }
    }
}
