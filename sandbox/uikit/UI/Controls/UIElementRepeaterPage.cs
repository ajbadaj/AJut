namespace AJutShowRoomUIKit.UI.Controls
{
    using System;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using AJut.UX.Controls;
    using UIKit;

    /// <summary>
    /// The UIElementRepeater demo: tiles in an ObservableCollection, shown in an AutoGrid through a repeater, the way WPF's
    /// docking shows a split's zones, with buttons that add, take away, move, and clear them.
    /// </summary>
    public sealed class UIElementRepeaterPage : ShowroomPageController
    {
        private const float kRepeaterHeight = 200f;
        private const int kStartingTileCount = 5;

        private readonly ObservableCollection<UIView> m_tiles = new ObservableCollection<UIView>();
        private readonly UILabel m_count = new UILabel();
        private int m_tilesMade;

        public UIElementRepeaterPage () : base("UIElementRepeater")
        {
        }

        protected override void BuildPage ()
        {
            this.AddHeading("UIElementRepeater");
            this.AddText("Shows a collection of views in a container and keeps them in step with it. Here the container is an AutoGrid three columns wide, and the collection is an ObservableCollection the buttons change.");

            this.AddDemoRow(_Button("Add", this.OnAdd), _Button("Take one", this.OnRemove), _Button("First to last", this.OnMove), _Button("Clear", this.OnClear));
            this.AddDemo(m_count);

            for (int index = 0; index < kStartingTileCount; ++index)
            {
                this.AddTile();
            }

            m_tiles.CollectionChanged += this.OnTilesChanged;
            var repeater = new UIElementRepeater { Container = new AutoGrid { FixedColumnCount = 3 }, DisplayChildren = m_tiles };
            repeater.HeightAnchor.ConstraintEqualTo(kRepeaterHeight).Active = true;
            this.AddWide(repeater);
            this.ShowCount();

            this.AddCode(@"
var tiles = new ObservableCollection<UIView>();
var repeater = new UIElementRepeater
{
    Container = new AutoGrid { FixedColumnCount = 3 },
    DisplayChildren = tiles,
};

// The grid follows the collection while the repeater is in a window
tiles.Add(tile);");
        }

        // The collection, the buttons and their handlers go with the page, so the handlers need no unhooking: nothing
        //  outside the page holds them
        private void OnTilesChanged (object sender, NotifyCollectionChangedEventArgs e) => this.ShowCount();

        private void OnAdd (object sender, EventArgs e) => this.AddTile();

        private void OnRemove (object sender, EventArgs e)
        {
            if (m_tiles.Count > 0)
            {
                m_tiles.RemoveAt(m_tiles.Count - 1);
            }
        }

        private void OnMove (object sender, EventArgs e)
        {
            if (m_tiles.Count > 1)
            {
                m_tiles.Move(0, m_tiles.Count - 1);
            }
        }

        private void OnClear (object sender, EventArgs e) => m_tiles.Clear();

        private void ShowCount () => m_count.Text = $"{m_tiles.Count} in the collection";

        private void AddTile ()
        {
            UIColor[] colors = { UIColor.SystemBlue, UIColor.SystemYellow, UIColor.SystemRed, UIColor.SystemGreen, UIColor.SystemPink, UIColor.SystemPurple, UIColor.SystemGray, UIColor.SystemBrown };
            m_tiles.Add(new UILabel
            {
                Text = $"Tile {m_tilesMade + 1}",
                TextAlignment = UITextAlignment.Center,
                BackgroundColor = colors[m_tilesMade % colors.Length],
                TextColor = UIColor.White,
            });

            ++m_tilesMade;
        }

        private static UIButton _Button (string title, EventHandler handler)
        {
            UIButton button = UIButton.FromType(UIButtonType.System);
            button.SetTitle(title, UIControlState.Normal);
            button.PrimaryActionTriggered += handler;
            return button;
        }
    }
}
