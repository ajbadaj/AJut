namespace AJutShowRoomUIKit.UI
{
    using UIKit;

    /// <summary>The code that built a demo, shown under it in a monospaced, selectable block.</summary>
    public sealed class ShowroomCodeDisplay : UITextView
    {
        public ShowroomCodeDisplay (string code)
        {
            const float kCornerRadius = 8f;
            const float kInset = 10f;
            const float kFontSize = 12f;

            this.Text = code.Trim();
            this.Font = UIFont.GetMonospacedSystemFont(kFontSize, UIFontWeight.Regular);
            this.TextColor = UIColor.Label;
            this.BackgroundColor = UIColor.SecondarySystemBackground;
            this.Editable = false;
            this.ScrollEnabled = false;
            this.TextContainerInset = new UIEdgeInsets(kInset, kInset, kInset, kInset);
            this.Layer.CornerRadius = kCornerRadius;
        }
    }
}
