namespace AJutShowRoomAppKit.UI.Controls
{
    using System;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using AJut.UX.Controls;
    using AppKit;

    // The UIElementRepeater demo: tiles in an ObservableCollection, shown in an AutoGrid through a repeater, the way WPF's
    // docking shows a split's zones, with buttons that add, take away, move, and clear them.
    public sealed class UIElementRepeaterPage : ShowroomPage
    {
        private const double kRepeaterHeight = 220.0;
        private const int kStartingTileCount = 5;

        private readonly ObservableCollection<NSView> m_tiles = new ObservableCollection<NSView>();
        private readonly NSTextField m_count;
        private readonly NSButton m_add;
        private readonly NSButton m_remove;
        private readonly NSButton m_move;
        private readonly NSButton m_clear;
        private int m_tilesMade;

        public UIElementRepeaterPage ()
            : base("UIElementRepeater", "Shows a collection of views in a container and keeps them in step with it. Here the container is an AutoGrid three columns wide, and the collection is an ObservableCollection the buttons change.")
        {
            m_add = MakeButton("Add");
            m_add.Activated += this.OnAdd;
            m_remove = MakeButton("Take one");
            m_remove.Activated += this.OnRemove;
            m_move = MakeButton("First to last");
            m_move.Activated += this.OnMove;
            m_clear = MakeButton("Clear");
            m_clear.Activated += this.OnClear;

            var buttons = new FillStack(NSUserInterfaceLayoutOrientation.Horizontal, spacing: 8.0);
            buttons.Add(m_add);
            buttons.Add(m_remove);
            buttons.Add(m_move);
            buttons.Add(m_clear);
            this.AddDemo(buttons, eFillStackCrossAlignment.Start);

            m_count = ShowroomStyle.Body(string.Empty);
            this.AddDemo(m_count);

            for (int index = 0; index < kStartingTileCount; ++index)
            {
                this.AddTile();
            }

            m_tiles.CollectionChanged += this.OnTilesChanged;
            var repeater = new UIElementRepeater { Container = new AutoGrid { FixedColumnCount = 3 }, DisplayChildren = m_tiles };
            repeater.TranslatesAutoresizingMaskIntoConstraints = false;
            repeater.HeightAnchor.ConstraintEqualTo((nfloat)kRepeaterHeight).Active = true;
            this.AddDemo(repeater);
            this.ShowCount();

            this.AddCode(
@"var tiles = new ObservableCollection<NSView>();
var repeater = new UIElementRepeater
{
    Container = new AutoGrid { FixedColumnCount = 3 },
    DisplayChildren = tiles,
};

// The grid follows the collection while the repeater is in a window
tiles.Add(tile);"
            );
        }

        protected override void OnTeardown ()
        {
            m_tiles.CollectionChanged -= this.OnTilesChanged;
            m_add.Activated -= this.OnAdd;
            m_remove.Activated -= this.OnRemove;
            m_move.Activated -= this.OnMove;
            m_clear.Activated -= this.OnClear;
        }

        // ===========[ Events ]================================================
        private void OnTilesChanged (object sender, NotifyCollectionChangedEventArgs e)
        {
            this.ShowCount();
        }

        private void OnAdd (object sender, EventArgs e)
        {
            this.AddTile();
        }

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

        private void OnClear (object sender, EventArgs e)
        {
            m_tiles.Clear();
        }

        // ===========[ Helper Methods ]========================================
        private void ShowCount ()
        {
            m_count.StringValue = $"{m_tiles.Count} in the collection";
        }

        private void AddTile ()
        {
            NSColor[] colors = { NSColor.SystemBlue, NSColor.SystemYellow, NSColor.SystemRed, NSColor.SystemGreen, NSColor.SystemPink, NSColor.SystemPurple, NSColor.SystemGray, NSColor.SystemBrown };
            var tile = new NSView { WantsLayer = true };
            tile.Layer.BackgroundColor = colors[m_tilesMade % colors.Length].CGColor;

            NSTextField label = NSTextField.CreateLabel($"Tile {m_tilesMade + 1}");
            label.TextColor = NSColor.White;
            label.TranslatesAutoresizingMaskIntoConstraints = false;
            tile.AddSubview(label);
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                label.CenterXAnchor.ConstraintEqualTo(tile.CenterXAnchor),
                label.CenterYAnchor.ConstraintEqualTo(tile.CenterYAnchor),
            });

            ++m_tilesMade;
            m_tiles.Add(tile);
        }

        private static NSButton MakeButton (string title)
        {
            return new NSButton { Title = title, BezelStyle = NSBezelStyle.Rounded };
        }
    }
}
