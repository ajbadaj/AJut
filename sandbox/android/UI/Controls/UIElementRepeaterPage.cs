namespace AJutShowRoomAndroid
{
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using AJut.UX.CodeBinding;
    using AJut.UX.Controls;
    using Android.Graphics;
    using Android.Views;
    using Android.Widget;

    /// <summary>
    /// The UIElementRepeater demo: tiles in an ObservableCollection, shown in an AutoGrid through a repeater, the way WPF's
    /// docking shows a split's zones, with buttons that add, take away, move, and clear them.
    /// </summary>
    public sealed class UIElementRepeaterPage : ShowroomPage
    {
        private const int kStartingTileCount = 5;

        private readonly ObservableCollection<View> m_tiles = new ObservableCollection<View>();
        private MainActivity? m_host;
        private TextView? m_count;
        private int m_tilesMade;

        public override string Title => "UIElementRepeater";

        public override int LayoutId => Resource.Layout.demo_ui_element_repeater;

        public override int MarkupSourceId => Resource.Raw.demo_ui_element_repeater_source;

        public override void Bind (View root, MainActivity host, BindingSet binder)
        {
            m_host = host;
            m_count = root.FindViewById<TextView>(Resource.Id.demo_repeater_count)!;
            m_tiles.Clear();
            m_tilesMade = 0;
            for (int index = 0; index < kStartingTileCount; ++index)
            {
                this.AddTile();
            }

            root.FindViewById<UIElementRepeater>(Resource.Id.demo_repeater)!.DisplayChildren = m_tiles;
            binder.OnClick(root.FindViewById(Resource.Id.demo_repeater_add)!, this.AddTile);
            binder.OnClick(root.FindViewById(Resource.Id.demo_repeater_remove)!, this.RemoveTile);
            binder.OnClick(root.FindViewById(Resource.Id.demo_repeater_move)!, this.MoveFirstToLast);
            binder.OnClick(root.FindViewById(Resource.Id.demo_repeater_clear)!, m_tiles.Clear);

            m_tiles.CollectionChanged -= this.OnTilesChanged;
            m_tiles.CollectionChanged += this.OnTilesChanged;
            this.ShowCount();
        }

        public override void Unbind ()
        {
            m_tiles.CollectionChanged -= this.OnTilesChanged;
            m_tiles.Clear();
            m_host = null;
            m_count = null;
        }

        private void OnTilesChanged (object? sender, NotifyCollectionChangedEventArgs e)
        {
            this.ShowCount();
        }

        private void ShowCount ()
        {
            if (m_count != null)
            {
                m_count.Text = $"{m_tiles.Count} in the collection";
            }
        }

        private void AddTile ()
        {
            if (m_host == null)
            {
                return;
            }

            uint[] colors = { 0xFF1E6FD9, 0xFFE0B020, 0xFFD03030, 0xFF2E9E4A, 0xFFE070A0, 0xFF8040C0, 0xFF808080, 0xFF8B5A2B };
            var tile = new TextView(m_host) { Text = $"Tile {m_tilesMade + 1}", Gravity = GravityFlags.Center };
            tile.SetBackgroundColor(new Color(unchecked((int)colors[m_tilesMade % colors.Length])));
            tile.SetTextColor(Color.White);
            ++m_tilesMade;
            m_tiles.Add(tile);
        }

        private void RemoveTile ()
        {
            if (m_tiles.Count > 0)
            {
                m_tiles.RemoveAt(m_tiles.Count - 1);
            }
        }

        private void MoveFirstToLast ()
        {
            if (m_tiles.Count > 1)
            {
                m_tiles.Move(0, m_tiles.Count - 1);
            }
        }
    }
}
