namespace AJut.UX.Controls
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Markup;
    using System.Windows.Media;
    using DPUtils = AJut.UX.DPUtils<AutoGrid>;
    using APUtils = AJut.UX.APUtils<AutoGrid>;
    using AJut.MathUtilities;
    using System.Windows.Data;

    public enum eAutoPopulationOrder
    {
        ColumnsFirst,
        RowsFirst,
        Auto
    }

    /// <summary>
    /// A grid which will automatically add rows and/or columns as child elements are inserted (and optionally grid resizers) based off of setup guidelines.
    /// </summary>
    public class AutoGrid : Grid
    {
        public static int kUnlimited = -1;

        private List<GridSplitter> m_generatedColumnSizers = new List<GridSplitter>();
        private List<GridSplitter> m_generatedRowSizers = new List<GridSplitter>();
        private bool m_isRemovingSizers = false;

        static AutoGrid ()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(AutoGrid), new FrameworkPropertyMetadata(typeof(AutoGrid)));
        }

        private static DependencyPropertyKey IsInRangePropertyKey = APUtils.RegisterReadOnly(GetIsInRange, SetIsInRange);
        public static DependencyProperty IsInRangeProperty = IsInRangePropertyKey.DependencyProperty;
        public static bool GetIsInRange (DependencyObject obj) => (bool)obj.GetValue(IsInRangeProperty);
        internal static void SetIsInRange (DependencyObject obj, bool value) => obj.SetValue(IsInRangePropertyKey, value);

        public static readonly DependencyProperty PaddingProperty = DPUtils.Register(_ => _.Padding);
        public Thickness Padding
        {
            get => (Thickness)this.GetValue(PaddingProperty);
            set => this.SetValue(PaddingProperty, value);
        }

        public static readonly DependencyProperty BorderBrushProperty = DPUtils.Register(_ => _.BorderBrush);
        public Brush BorderBrush
        {
            get => (Brush)this.GetValue(BorderBrushProperty);
            set => this.SetValue(BorderBrushProperty, value);
        }

        public static readonly DependencyProperty BorderThicknessProperty = DPUtils.Register(_ => _.BorderThickness);
        public Thickness BorderThickness
        {
            get => (Thickness)this.GetValue(BorderThicknessProperty);
            set => this.SetValue(BorderThicknessProperty, value);
        }

        public static readonly DependencyProperty FixedColumnCountProperty = DPUtils.Register(_ => _.FixedColumnCount, AutoGrid.kUnlimited, (d,e)=>d.EvaluateAndSetSetRowColumnInfo());
        public int FixedColumnCount
        {
            get => (int)this.GetValue(FixedColumnCountProperty);
            set => this.SetValue(FixedColumnCountProperty, value);
        }

        public static readonly DependencyProperty FixedRowCountProperty = DPUtils.Register(_ => _.FixedRowCount, AutoGrid.kUnlimited, (d, e) => d.EvaluateAndSetSetRowColumnInfo());
        public int FixedRowCount
        {
            get => (int)this.GetValue(FixedRowCountProperty);
            set => this.SetValue(FixedRowCountProperty, value);
        }

        public static readonly DependencyProperty AutoPopulationOrderProperty = DPUtils.Register(_ => _.AutoPopulationOrder, (d, e) => d.EvaluateAndSetSetRowColumnInfo());
        public eAutoPopulationOrder AutoPopulationOrder
        {
            get => (eAutoPopulationOrder)this.GetValue(AutoPopulationOrderProperty);
            set => this.SetValue(AutoPopulationOrderProperty, value);
        }

        public static readonly DependencyProperty AddColumnResizersProperty = DPUtils.Register(_ => _.AddColumnResizers, (d, e) => d.EvaluateAndSetSetRowColumnInfo());
        public bool AddColumnResizers
        {
            get => (bool)this.GetValue(AddColumnResizersProperty);
            set => this.SetValue(AddColumnResizersProperty, value);
        }

        public static readonly DependencyProperty AddRowResizersProperty = DPUtils.Register(_ => _.AddRowResizers, (d,e)=> d.EvaluateAndSetSetRowColumnInfo());
        public bool AddRowResizers
        {
            get => (bool)this.GetValue(AddRowResizersProperty);
            set => this.SetValue(AddRowResizersProperty, value);
        }

        public static readonly DependencyProperty SizerLengthProperty = DPUtils.Register(_ => _.SizerLength, 2);
        public double SizerLength
        {
            get => (double)this.GetValue(SizerLengthProperty);
            set => this.SetValue(SizerLengthProperty, value);
        }

        public static readonly DependencyProperty InitialRowHeightProperty = DPUtils.Register(_ => _.InitialRowHeight, new GridLength(1.0, GridUnitType.Star));
        public GridLength InitialRowHeight
        {
            get => (GridLength)this.GetValue(InitialRowHeightProperty);
            set => this.SetValue(InitialRowHeightProperty, value);
        }

        public static readonly DependencyProperty InitialColumnWidthProperty = DPUtils.Register(_ => _.InitialColumnWidth, new GridLength(1.0, GridUnitType.Star));
        public GridLength InitialColumnWidth
        {
            get => (GridLength)this.GetValue(InitialColumnWidthProperty);
            set => this.SetValue(InitialColumnWidthProperty, value);
        }

        public static readonly DependencyProperty ShrinkRowColumnsToUtilizedElementCountProperty = DPUtils.Register(_ => _.ShrinkRowColumnsToUtilizedElementCount, (d, e) => d.EvaluateAndSetSetRowColumnInfo());
        public bool ShrinkRowColumnsToUtilizedElementCount
        {
            get => (bool)this.GetValue(ShrinkRowColumnsToUtilizedElementCountProperty);
            set => this.SetValue(ShrinkRowColumnsToUtilizedElementCountProperty, value);
        }

        public static readonly DependencyProperty MinElementWidthProperty = DPUtils.Register(_ => _.MinElementWidth, 10.0);
        public double MinElementWidth
        {
            get => (double)this.GetValue(MinElementWidthProperty);
            set => this.SetValue(MinElementWidthProperty, value);
        }

        public static readonly DependencyProperty MinElementHeightProperty = DPUtils.Register(_ => _.MinElementHeight, 10.0);
        public double MinElementHeight
        {
            get => (double)this.GetValue(MinElementHeightProperty);
            set => this.SetValue(MinElementHeightProperty, value);
        }

        protected override void OnVisualChildrenChanged (DependencyObject visualAdded, DependencyObject visualRemoved)
        {
            base.OnVisualChildrenChanged(visualAdded, visualRemoved);
            this.EvaluateAndSetSetRowColumnInfo();
        }

        protected virtual void EvaluateAndSetSetRowColumnInfo ()
        { 
            if (m_isRemovingSizers)
            {
                return;
            }

            // Reduce definitions to their unit versions (300*, 300*, 100* → 3*, 3*, 1*)
            if (this.RowDefinitions.Count > 1 && this.RowDefinitions.All(r => r.Height.IsStar))
            {
                double unitBase = Calculate.Mean(this.RowDefinitions.Select(r => r.Height.Value));
                this.RowDefinitions.ForEach(r => r.Height = new GridLength(r.Height.Value / unitBase, GridUnitType.Star));
            }

            if (this.ColumnDefinitions.Count > 1 && this.ColumnDefinitions.All(c => c.Width.IsStar))
            {
                double unitBase = Calculate.Mean(this.ColumnDefinitions.Select(r => r.Width.Value));
                this.ColumnDefinitions.ForEach(c => c.Width = new GridLength(c.Width.Value / unitBase, GridUnitType.Star));
            }

            // Grow row/column definitions to match current set, and set current items to have proper Grid.Row/Grid.Column
            int maxRowIndex = 0;
            int maxColumnIndex = 0;
            int evaluationIndex = 0;
            for (int index = 0; index < this.Children.Count; ++index)
            {
                UIElement child = this.Children[index];
                if (child == null || child is GridSplitter)
                {
                    continue;
                }

                if (this.CalculateRowColumnForIndex(evaluationIndex, out int row, out int column))
                {
                    this.EnsureRowColumnAvailable(row, column);
                    Grid.SetRow(child, row);
                    Grid.SetColumn(child, column);
                    AutoGrid.SetIsInRange(child, true);

                    if (row > maxRowIndex)
                    {
                        maxRowIndex = row;
                    }

                    if (column > maxColumnIndex)
                    {
                        maxColumnIndex = column;
                    }
                }
                else
                {
                    AutoGrid.SetIsInRange(child, false);
                }

                ++evaluationIndex;
            }

            // ========== [ Sizer Stuff ]=========================
            var sizersToAdd = new List<GridSplitter>();
            var sizersToRemove = new List<GridSplitter>();

            // Next add any sizers needed
            int columnCount = this.FixedColumnCount == kUnlimited ? maxColumnIndex + 1 : this.FixedColumnCount;
            int rowCount = this.FixedRowCount == kUnlimited ? maxRowIndex + 1 : this.FixedRowCount;
            if (this.AddColumnResizers && this.FixedColumnCount != 0
                && columnCount > 0 && rowCount > 0)
            {
                for (int column = 0; column < columnCount - 1; ++column)
                {
                    // We already have a column splitter for that
                    if (column >= m_generatedColumnSizers.Count)
                    {
                        GridSplitter gs = new GridSplitter
                        {
                            HorizontalAlignment = HorizontalAlignment.Right,
                            VerticalAlignment = VerticalAlignment.Stretch,
                            ResizeDirection = GridResizeDirection.Columns,
                        };

                        MultiBinding binding = new MultiBinding();
                        gs.SetBinding(GridSplitter.WidthProperty, this.CreateBinding(SizerLengthProperty, System.Windows.Data.BindingMode.OneWay));

                        m_generatedColumnSizers.Add(gs);
                        sizersToAdd.Add(gs);
                    }

                    Grid.SetRow(m_generatedColumnSizers[column], 0);
                    Grid.SetColumn(m_generatedColumnSizers[column], column);
                    Grid.SetRowSpan(m_generatedColumnSizers[column], rowCount);
                }
            }
            else
            {
                sizersToRemove.AddEach(m_generatedColumnSizers);
                m_generatedColumnSizers.Clear();
            }

            if (this.AddRowResizers && this.FixedRowCount != 0
                && columnCount > 0 && rowCount > 0)
            {
                for (int row = 0; row < rowCount - 1; ++row)
                {
                    // We already have a row splitter for that
                    if (row >= m_generatedRowSizers.Count)
                    {
                        GridSplitter gs = new GridSplitter
                        {
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                            VerticalAlignment = VerticalAlignment.Bottom,
                            ResizeDirection = GridResizeDirection.Rows,
                        };
                        gs.SetBinding(GridSplitter.HeightProperty, this.CreateBinding(SizerLengthProperty, System.Windows.Data.BindingMode.OneWay));

                        m_generatedRowSizers.Add(gs);
                        sizersToAdd.Add(gs);
                    }

                    Grid.SetRow(m_generatedRowSizers[row], row);
                    Grid.SetColumn(m_generatedRowSizers[row], 0);
                    Grid.SetColumnSpan(m_generatedRowSizers[row], columnCount);
                }
            }
            else
            {
                sizersToRemove.AddEach(m_generatedRowSizers);
                m_generatedRowSizers.Clear();
            }

            // If we've reduced items, then we may have more rows or columns than we're using. If the caller
            // wants to, we should cut back accordingly. Otherwise we need still to shrink to the max allowed
            if (!this.ShrinkRowColumnsToUtilizedElementCount)
            {
                this.EnsureRowColumnAvailable(this.FixedRowCount - 1, this.FixedColumnCount - 1);
                if (this.FixedRowCount != kUnlimited && this.FixedColumnCount != kUnlimited)
                {
                    maxRowIndex = this.FixedRowCount - 1;
                    maxColumnIndex = this.FixedColumnCount - 1;
                }
            }

            int reduction = this.RowDefinitions.Count - maxRowIndex - 1;
            while (reduction > 0)
            {
                this.RowDefinitions.RemoveAt(this.RowDefinitions.Count - reduction);
                --reduction;
            }

            for (int index = this.RowDefinitions.Count; index >= 0 && index < m_generatedRowSizers.Count; --index)
            {
                sizersToRemove.Add(m_generatedRowSizers[index]);
                m_generatedRowSizers.RemoveAt(index);
            }

            reduction = this.ColumnDefinitions.Count - maxColumnIndex - 1;
            while (reduction > 0)
            {
                this.ColumnDefinitions.RemoveAt(this.ColumnDefinitions.Count - reduction);
                --reduction;
            }

            for (int index = this.ColumnDefinitions.Count; index >= 0 && index < m_generatedColumnSizers.Count; --index)
            {
                sizersToRemove.Add(m_generatedColumnSizers[index]);
                m_generatedColumnSizers.RemoveAt(index);
            }

            if (sizersToAdd.Any() || sizersToRemove.Any())
            {
                this.Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        m_isRemovingSizers = true;
                        foreach (var sizer in sizersToRemove)
                        {
                            this.Children.Remove(sizer);
                        }

                        foreach (var sizer in sizersToAdd)
                        {
                            this.Children.Add(sizer);
                        }
                    }
                    finally
                    {
                        m_isRemovingSizers = false;
                    }
                });
            }
        }

        protected virtual void EnsureRowColumnAvailable (int row, int column)
        {
            while (this.RowDefinitions.Count <= row)
            {
                var def = new RowDefinition { Height = this.InitialRowHeight };
                def.SetBinding(RowDefinition.MinHeightProperty, this.CreateBinding(AutoGrid.MinElementHeightProperty));
                this.RowDefinitions.Add(def);
            }
            while (this.ColumnDefinitions.Count <= column)
            {
                var def = new ColumnDefinition { Width = this.InitialColumnWidth };
                def.SetBinding(ColumnDefinition.MinWidthProperty, this.CreateBinding(AutoGrid.MinElementWidthProperty));
                this.ColumnDefinitions.Add(def);
            }
        }

        protected virtual bool CalculateRowColumnForIndex(int index, out int row, out int column)
        {
            int maxColumns = this.FixedColumnCount;
            int maxRows = this.FixedRowCount;
            if (maxColumns == 0 || maxRows == 0
                || (index >= (_IsFixed(maxColumns) && _IsFixed(maxRows) ? (maxColumns * maxRows) : int.MaxValue))
            )
            {
                row = -1;
                column = -1;
                return false;
            }
            eAutoPopulationOrder effectiveOrder = this.AutoPopulationOrder;
            if (effectiveOrder == eAutoPopulationOrder.Auto)
            {
                if (!_IsFixed(maxRows) && _IsFixed(maxColumns))
                {
                    effectiveOrder = eAutoPopulationOrder.ColumnsFirst;
                }
                else if (_IsFixed(maxRows) && !_IsFixed(maxColumns))
                {
                    effectiveOrder = eAutoPopulationOrder.RowsFirst;
                }
                else // Just default to columns first
                {
                    effectiveOrder = eAutoPopulationOrder.ColumnsFirst;
                }
            }

            if (effectiveOrder == eAutoPopulationOrder.ColumnsFirst)
            {
                row = 0;
                column = index;
                if (!_IsFixed(maxColumns))
                {
                    return true;
                }

                while (column >= maxColumns)
                {
                    ++row;
                    column = column - maxColumns;
                }

                return true;
            }

            row = index;
            column = 0;
            if (!_IsFixed(maxRows))
            {
                return true;
            }

            while (row >= maxRows)
            {
                ++column;
                row = row - maxRows;
            }

            return true;
            bool _IsFixed (int _target) => _target != kUnlimited;
        }
    }
}
