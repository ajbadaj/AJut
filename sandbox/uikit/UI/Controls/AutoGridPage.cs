namespace AJutShowRoomUIKit.UI.Controls
{
    using System;
    using AJut.UX.Controls;
    using UIKit;

    /// <summary>
    /// The AutoGrid demo, as WPF's: colored tiles filling the grid in order, with the fill order, the fixed counts,
    /// shrinking and the resizers to change, and tiles to add and take away so the grid grows and shrinks.
    /// </summary>
    public sealed class AutoGridPage : ShowroomPageController
    {
        private const float kGridHeight = 240f;
        private const int kStartingTileCount = 8;

        private readonly AutoGrid m_grid = new AutoGrid { FixedColumnCount = 3 };
        private readonly UISegmentedControl m_order = new UISegmentedControl("Columns first", "Rows first", "Auto");
        private readonly UIStepper m_rows = new UIStepper { MinimumValue = -1, MaximumValue = 6, Value = -1 };
        private readonly UIStepper m_columns = new UIStepper { MinimumValue = -1, MaximumValue = 6, Value = 3 };
        private readonly UILabel m_counts = new UILabel();
        private readonly UISwitch m_shrink = new UISwitch();
        private readonly UISwitch m_rowResizers = new UISwitch();
        private readonly UISwitch m_columnResizers = new UISwitch();
        private readonly UISwitch m_border = new UISwitch();
        private int m_tileCount;

        public AutoGridPage () : base("AutoGrid")
        {
        }

        protected override void BuildPage ()
        {
            this.AddHeading("AutoGrid");
            this.AddText("A grid that adds rows and columns as subviews come in, putting each in the next cell and wrapping at a fixed column or row count (-1 for none). With both counts fixed, a tile past the last cell has no cell and is not shown. With resizers on, drag one to trade space between the two rows or columns beside it.");

            m_order.SelectedSegment = 0;
            m_order.ValueChanged += this.OnSettingChanged;
            this.AddDemo(m_order);

            m_rows.ValueChanged += this.OnSettingChanged;
            m_columns.ValueChanged += this.OnSettingChanged;
            this.AddDemoRow(new UILabel { Text = "Rows" }, m_rows, new UILabel { Text = "Columns" }, m_columns);
            this.AddDemo(m_counts);

            m_shrink.ValueChanged += this.OnSettingChanged;
            m_rowResizers.ValueChanged += this.OnSettingChanged;
            m_columnResizers.ValueChanged += this.OnSettingChanged;
            this.AddDemoRow(new UILabel { Text = "Shrink" }, m_shrink, new UILabel { Text = "Row resizers" }, m_rowResizers);
            m_border.ValueChanged += this.OnSettingChanged;
            this.AddDemoRow(new UILabel { Text = "Column resizers" }, m_columnResizers, new UILabel { Text = "Border" }, m_border);

            UIButton add = UIButton.FromType(UIButtonType.System);
            add.SetTitle("Add a tile", UIControlState.Normal);
            add.PrimaryActionTriggered += this.OnAddTile;
            UIButton remove = UIButton.FromType(UIButtonType.System);
            remove.SetTitle("Take one away", UIControlState.Normal);
            remove.PrimaryActionTriggered += this.OnRemoveTile;
            this.AddDemoRow(add, remove);

            for (int index = 0; index < kStartingTileCount; ++index)
            {
                this.AddTile();
            }

            m_grid.HeightAnchor.ConstraintEqualTo(kGridHeight).Active = true;
            this.AddWide(m_grid);
            this.ApplySettings();

            this.AddCode(@"
var grid = new AutoGrid { FixedColumnCount = 3, AddColumnResizers = true };
grid.AddSubview(tile);

// With both counts fixed, a subview past the last cell has none
bool shown = grid.GetIsInRange(tile);");
        }

        // The controls and their handlers go with the page, so the handlers need no unhooking: nothing native holds the page
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
            UIView[] tiles = m_grid.GridChildren;
            if (tiles.Length > 0)
            {
                tiles[tiles.Length - 1].RemoveFromSuperview();
                --m_tileCount;
                this.ApplySettings();
            }
        }

        private void ApplySettings ()
        {
            m_grid.AutoPopulationOrder = (eAutoPopulationOrder)(int)m_order.SelectedSegment;
            m_grid.FixedRowCount = (int)m_rows.Value;
            m_grid.FixedColumnCount = (int)m_columns.Value;
            m_grid.ShrinkRowColumnsToUtilizedElementCount = m_shrink.On;
            m_grid.AddRowResizers = m_rowResizers.On;
            m_grid.AddColumnResizers = m_columnResizers.On;
            nfloat border = m_border.On ? 2 : 0;
            m_grid.BorderThickness = new UIEdgeInsets(border, border, border, border);
            m_counts.Text = $"Fixed rows {(int)m_rows.Value}, fixed columns {(int)m_columns.Value}";

            // A tile with no cell is hidden, as WPF's demo hides one through AutoGrid.IsInRange
            foreach (UIView tile in m_grid.GridChildren)
            {
                tile.Hidden = !m_grid.GetIsInRange(tile);
            }
        }

        private void AddTile ()
        {
            UIColor[] colors = { UIColor.SystemBlue, UIColor.SystemYellow, UIColor.SystemRed, UIColor.SystemGreen, UIColor.SystemPink, UIColor.SystemPurple, UIColor.SystemGray, UIColor.SystemBrown };
            var tile = new UILabel
            {
                Text = $"Tile {m_tileCount + 1}",
                TextAlignment = UITextAlignment.Center,
                BackgroundColor = colors[m_tileCount % colors.Length],
                TextColor = UIColor.White,
            };

            ++m_tileCount;
            m_grid.AddSubview(tile);
        }
    }
}
