namespace AJutShowRoomAppKit.UI.Controls
{
    using System;
    using AJut.UX.Controls;
    using AppKit;
    using CoreGraphics;

    // The AutoGrid demo, as WPF's: colored tiles filling the grid in order, with the fill order, the fixed counts,
    // shrinking and the resizers to change, and tiles to add and take away so the grid grows and shrinks.
    public sealed class AutoGridPage : ShowroomPage
    {
        private const double kGridHeight = 260.0;
        private const int kStartingTileCount = 8;
        private const int kMaxCount = 6;

        private readonly AutoGrid m_grid = new AutoGrid { FixedColumnCount = 3 };
        private readonly NSSegmentedControl m_order;
        private readonly NSStepper m_rows;
        private readonly NSStepper m_columns;
        private readonly NSTextField m_counts;
        private readonly NSButton m_shrink;
        private readonly NSButton m_rowResizers;
        private readonly NSButton m_columnResizers;
        private readonly NSButton m_border;
        private readonly NSButton m_add;
        private readonly NSButton m_remove;
        private int m_tileCount;

        public AutoGridPage ()
            : base("AutoGrid", "A grid that adds rows and columns as subviews come in, putting each in the next cell and wrapping at a fixed column or row count (-1 for none). With both counts fixed, a tile past the last cell has no cell and is hidden here. With resizers on, drag one to trade space between the two rows or columns beside it.")
        {
            m_order = new NSSegmentedControl { SegmentCount = 3, SelectedSegment = 0 };
            m_order.SetLabel("Columns first", 0);
            m_order.SetLabel("Rows first", 1);
            m_order.SetLabel("Auto", 2);
            m_order.Activated += this.OnSettingChanged;
            this.AddLabeledDemo("Fill order", m_order);

            m_rows = MakeStepper(-1);
            m_rows.Activated += this.OnSettingChanged;
            this.AddLabeledDemo("Fixed rows", m_rows);
            m_columns = MakeStepper(3);
            m_columns.Activated += this.OnSettingChanged;
            this.AddLabeledDemo("Fixed columns", m_columns);
            m_counts = ShowroomStyle.Body(string.Empty);
            this.AddDemo(m_counts);

            m_shrink = MakeCheckbox("Shrink to the tiles used");
            m_shrink.Activated += this.OnSettingChanged;
            this.AddDemo(m_shrink, eFillStackCrossAlignment.Start);
            m_rowResizers = MakeCheckbox("Row resizers");
            m_rowResizers.Activated += this.OnSettingChanged;
            this.AddDemo(m_rowResizers, eFillStackCrossAlignment.Start);
            m_columnResizers = MakeCheckbox("Column resizers");
            m_columnResizers.Activated += this.OnSettingChanged;
            this.AddDemo(m_columnResizers, eFillStackCrossAlignment.Start);
            m_border = MakeCheckbox("Border (2pt, the theme's border color)");
            m_border.Activated += this.OnSettingChanged;
            this.AddDemo(m_border, eFillStackCrossAlignment.Start);

            m_add = new NSButton { Title = "Add a tile", BezelStyle = NSBezelStyle.Rounded };
            m_add.Activated += this.OnAddTile;
            this.AddDemo(m_add, eFillStackCrossAlignment.Start);
            m_remove = new NSButton { Title = "Take one away", BezelStyle = NSBezelStyle.Rounded };
            m_remove.Activated += this.OnRemoveTile;
            this.AddDemo(m_remove, eFillStackCrossAlignment.Start);

            for (int index = 0; index < kStartingTileCount; ++index)
            {
                this.AddTile();
            }

            // A FillStack gives a child with no height of its own none, so the grid gets one
            m_grid.TranslatesAutoresizingMaskIntoConstraints = false;
            m_grid.HeightAnchor.ConstraintEqualTo((nfloat)kGridHeight).Active = true;
            this.AddDemo(m_grid);
            this.ApplySettings();

            this.AddCode(
@"var grid = new AutoGrid { FixedColumnCount = 3, AddColumnResizers = true };
grid.AddSubview(tile);

// With both counts fixed, a subview past the last cell has none
bool shown = grid.GetIsInRange(tile);"
            );
        }

        protected override void OnTeardown ()
        {
            m_order.Activated -= this.OnSettingChanged;
            m_rows.Activated -= this.OnSettingChanged;
            m_columns.Activated -= this.OnSettingChanged;
            m_shrink.Activated -= this.OnSettingChanged;
            m_rowResizers.Activated -= this.OnSettingChanged;
            m_columnResizers.Activated -= this.OnSettingChanged;
            m_border.Activated -= this.OnSettingChanged;
            m_add.Activated -= this.OnAddTile;
            m_remove.Activated -= this.OnRemoveTile;
        }

        // ===========[ Events ]================================================
        private void OnSettingChanged (object sender, EventArgs e)
        {
            this.ApplySettings();
        }

        private void OnAddTile (object sender, EventArgs e)
        {
            this.AddTile();
            this.ApplySettings();
        }

        private void OnRemoveTile (object sender, EventArgs e)
        {
            NSView[] tiles = m_grid.GridChildren;
            if (tiles.Length > 0)
            {
                tiles[tiles.Length - 1].RemoveFromSuperview();
                --m_tileCount;
                this.ApplySettings();
            }
        }

        // ===========[ Helper Methods ]========================================
        private void ApplySettings ()
        {
            m_grid.AutoPopulationOrder = (eAutoPopulationOrder)(int)m_order.SelectedSegment;
            m_grid.FixedRowCount = m_rows.IntValue;
            m_grid.FixedColumnCount = m_columns.IntValue;
            m_grid.ShrinkRowColumnsToUtilizedElementCount = m_shrink.State == NSCellStateValue.On;
            m_grid.AddRowResizers = m_rowResizers.State == NSCellStateValue.On;
            m_grid.AddColumnResizers = m_columnResizers.State == NSCellStateValue.On;
            nfloat border = (m_border.State == NSCellStateValue.On) ? 2 : 0;
            m_grid.BorderThickness = new NSEdgeInsets(border, border, border, border);
            m_counts.StringValue = $"Fixed rows {m_rows.IntValue}, fixed columns {m_columns.IntValue}";

            // A tile with no cell is hidden, as WPF's demo hides one through AutoGrid.IsInRange
            foreach (NSView tile in m_grid.GridChildren)
            {
                tile.Hidden = !m_grid.GetIsInRange(tile);
            }
        }

        private void AddTile ()
        {
            NSColor[] colors = { NSColor.SystemBlue, NSColor.SystemYellow, NSColor.SystemRed, NSColor.SystemGreen, NSColor.SystemPink, NSColor.SystemPurple, NSColor.SystemGray, NSColor.SystemBrown };
            var tile = new NSView { WantsLayer = true };
            tile.Layer.BackgroundColor = colors[m_tileCount % colors.Length].CGColor;

            NSTextField label = NSTextField.CreateLabel($"Tile {m_tileCount + 1}");
            label.TextColor = NSColor.White;
            label.TranslatesAutoresizingMaskIntoConstraints = false;
            tile.AddSubview(label);
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                label.CenterXAnchor.ConstraintEqualTo(tile.CenterXAnchor),
                label.CenterYAnchor.ConstraintEqualTo(tile.CenterYAnchor),
            });

            ++m_tileCount;
            m_grid.AddSubview(tile);
        }

        private static NSStepper MakeStepper (int value)
        {
            return new NSStepper { MinValue = -1, MaxValue = kMaxCount, Increment = 1, IntValue = value, ValueWraps = false };
        }

        private static NSButton MakeCheckbox (string title)
        {
            var checkbox = new NSButton { Title = title };
            checkbox.SetButtonType(NSButtonType.Switch);
            return checkbox;
        }
    }
}
