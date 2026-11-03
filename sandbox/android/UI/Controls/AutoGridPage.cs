namespace AJutShowRoomAndroid
{
    using System;
    using System.ComponentModel;
    using AJut;
    using AJut.UX.CodeBinding;
    using AJut.UX.Controls;
    using Android.Graphics;
    using Android.Views;
    using Android.Widget;

    /// <summary>
    /// The AutoGrid demo, as WPF's: colored tiles filling the grid in order, with the fill order, the fixed counts,
    /// shrinking and the resizers to change, and tiles to add and take away so the grid grows and shrinks.
    /// </summary>
    public sealed class AutoGridPage : ShowroomPage
    {
        private const int kStartingTileCount = 8;

        private readonly GridSettings m_settings = new GridSettings();
        private AutoGrid? m_grid;
        private MainActivity? m_host;
        private int m_tileCount;

        public override string Title => "AutoGrid";

        public override int LayoutId => Resource.Layout.demo_auto_grid;

        public override int MarkupSourceId => Resource.Raw.demo_auto_grid_source;

        public override void Bind (View root, MainActivity host, BindingSet binder)
        {
            m_host = host;
            m_grid = root.FindViewById<AutoGrid>(Resource.Id.demo_auto_grid)!;
            m_tileCount = 0;
            for (int index = 0; index < kStartingTileCount; ++index)
            {
                this.AddTile();
            }

            binder.BindSelectedValue(root.FindViewById<EnumComboBox>(Resource.Id.demo_auto_grid_order)!, m_settings, nameof(GridSettings.Order), () => m_settings.Order, value => m_settings.Order = value);
            binder.BindValue(root.FindViewById<NumericEditor>(Resource.Id.demo_auto_grid_rows)!, m_settings, nameof(GridSettings.FixedRowCount), () => m_settings.FixedRowCount, value => m_settings.FixedRowCount = value);
            binder.BindValue(root.FindViewById<NumericEditor>(Resource.Id.demo_auto_grid_columns)!, m_settings, nameof(GridSettings.FixedColumnCount), () => m_settings.FixedColumnCount, value => m_settings.FixedColumnCount = value);
            binder.BindChecked(root.FindViewById<CheckBox>(Resource.Id.demo_auto_grid_shrink)!, m_settings, nameof(GridSettings.Shrink), () => m_settings.Shrink, value => m_settings.Shrink = value);
            binder.BindChecked(root.FindViewById<CheckBox>(Resource.Id.demo_auto_grid_row_resizers)!, m_settings, nameof(GridSettings.RowResizers), () => m_settings.RowResizers, value => m_settings.RowResizers = value);
            binder.BindChecked(root.FindViewById<CheckBox>(Resource.Id.demo_auto_grid_column_resizers)!, m_settings, nameof(GridSettings.ColumnResizers), () => m_settings.ColumnResizers, value => m_settings.ColumnResizers = value);
            binder.BindChecked(root.FindViewById<CheckBox>(Resource.Id.demo_auto_grid_border)!, m_settings, nameof(GridSettings.Border), () => m_settings.Border, value => m_settings.Border = value);
            binder.OnClick(root.FindViewById(Resource.Id.demo_auto_grid_add)!, this.AddTileAndApply);
            binder.OnClick(root.FindViewById(Resource.Id.demo_auto_grid_remove)!, this.RemoveTile);

            m_settings.PropertyChanged -= this.OnSettingsChanged;
            m_settings.PropertyChanged += this.OnSettingsChanged;
            this.ApplySettings();
        }

        public override void Unbind ()
        {
            m_settings.PropertyChanged -= this.OnSettingsChanged;
            m_grid = null;
            m_host = null;
        }

        private void OnSettingsChanged (object? sender, PropertyChangedEventArgs e)
        {
            this.ApplySettings();
        }

        private void ApplySettings ()
        {
            if (m_grid == null)
            {
                return;
            }

            m_grid.AutoPopulationOrder = m_settings.Order;
            m_grid.FixedRowCount = m_settings.FixedRowCount;
            m_grid.FixedColumnCount = m_settings.FixedColumnCount;
            m_grid.ShrinkRowColumnsToUtilizedElementCount = m_settings.Shrink;
            m_grid.AddRowResizers = m_settings.RowResizers;
            m_grid.AddColumnResizers = m_settings.ColumnResizers;
            int border = m_settings.Border ? (int)Math.Round(2 * m_host!.Resources!.DisplayMetrics!.Density) : 0;
            m_grid.SetBorderThickness(border, border, border, border);

            // A tile with no cell is hidden, as WPF's demo hides one through AutoGrid.IsInRange
            for (int index = 0; index < m_grid.ChildCount; ++index)
            {
                View tile = m_grid.GetChildAt(index)!;
                tile.Visibility = m_grid.GetIsInRange(tile) ? ViewStates.Visible : ViewStates.Gone;
            }
        }

        private void AddTileAndApply ()
        {
            this.AddTile();
            this.ApplySettings();
        }

        private void RemoveTile ()
        {
            if ((m_grid != null) && (m_grid.ChildCount > 0))
            {
                m_grid.RemoveViewAt(m_grid.ChildCount - 1);
                --m_tileCount;
                this.ApplySettings();
            }
        }

        private void AddTile ()
        {
            if ((m_grid == null) || (m_host == null))
            {
                return;
            }

            uint[] colors = { 0xFF1E6FD9, 0xFFE0B020, 0xFFD03030, 0xFF2E9E4A, 0xFFE070A0, 0xFF8040C0, 0xFF808080, 0xFF8B5A2B };
            var tile = new TextView(m_host)
            {
                Text = $"Tile {m_tileCount + 1}",
                Gravity = GravityFlags.Center,
            };

            tile.SetBackgroundColor(new Color(unchecked((int)colors[m_tileCount % colors.Length])));
            tile.SetTextColor(Color.White);
            ++m_tileCount;
            m_grid.AddView(tile);
        }

        // ===========[ Subclasses/structs ]===================================
        private sealed class GridSettings : NotifyPropertyChanged
        {
            private eAutoPopulationOrder m_order = eAutoPopulationOrder.ColumnsFirst;
            private int m_fixedRowCount = AutoGridLayout.kUnlimited;
            private int m_fixedColumnCount = 3;
            private bool m_shrink;
            private bool m_rowResizers;
            private bool m_columnResizers;
            private bool m_border;

            public eAutoPopulationOrder Order
            {
                get => m_order;
                set => this.SetAndRaiseIfChanged(ref m_order, value);
            }

            public int FixedRowCount
            {
                get => m_fixedRowCount;
                set => this.SetAndRaiseIfChanged(ref m_fixedRowCount, value);
            }

            public int FixedColumnCount
            {
                get => m_fixedColumnCount;
                set => this.SetAndRaiseIfChanged(ref m_fixedColumnCount, value);
            }

            public bool Shrink
            {
                get => m_shrink;
                set => this.SetAndRaiseIfChanged(ref m_shrink, value);
            }

            public bool RowResizers
            {
                get => m_rowResizers;
                set => this.SetAndRaiseIfChanged(ref m_rowResizers, value);
            }

            public bool ColumnResizers
            {
                get => m_columnResizers;
                set => this.SetAndRaiseIfChanged(ref m_columnResizers, value);
            }

            public bool Border
            {
                get => m_border;
                set => this.SetAndRaiseIfChanged(ref m_border, value);
            }
        }
    }
}
