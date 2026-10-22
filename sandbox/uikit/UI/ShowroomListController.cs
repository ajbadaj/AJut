namespace AJutShowRoomUIKit.UI
{
    using System.Collections.Generic;
    using Foundation;
    using UIKit;

    /// <summary>
    /// The one navigation pattern every level of the showroom uses: a plain table of entries, each pushing what it
    /// opens onto the navigation stack. The top level lists the areas, and each area lists its pages.
    /// </summary>
    public class ShowroomListController : UITableViewController
    {
        private const string kCellId = "ShowroomEntryCell";
        private readonly IReadOnlyList<ShowroomEntry> m_entries;

        public ShowroomListController (string title, IReadOnlyList<ShowroomEntry> entries) : base(UITableViewStyle.InsetGrouped)
        {
            this.Title = title;
            m_entries = entries;
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad();
            this.TableView.RegisterClassForCellReuse(typeof(UITableViewCell), kCellId);
        }

        public override nint RowsInSection (UITableView tableView, nint section) => m_entries.Count;

        public override UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
        {
            UITableViewCell cell = tableView.DequeueReusableCell(kCellId, indexPath);
            UIListContentConfiguration content = cell.DefaultContentConfiguration;
            content.Text = m_entries[indexPath.Row].Title;
            cell.ContentConfiguration = content;
            cell.Accessory = UITableViewCellAccessory.DisclosureIndicator;
            return cell;
        }

        public override void RowSelected (UITableView tableView, NSIndexPath indexPath)
        {
            tableView.DeselectRow(indexPath, true);
            this.NavigationController?.PushViewController(m_entries[indexPath.Row].Open(), true);
        }
    }

    /// <summary>The top of the showroom: the list of areas.</summary>
    public sealed class ShowroomAreaListController : ShowroomListController
    {
        public ShowroomAreaListController () : base("AJut Show Room", ShowroomMap.Areas)
        {
        }
    }
}
