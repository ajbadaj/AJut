namespace AJutShowRoomAppKit.UI
{
    using System;
    using AJut.UX.Controls;
    using AJut.UX.Theming;
    using AppKit;

    // The code beside each example, in a monospace block with a copy button. The AppKit twin of
    // the WinUI3 showroom's ShowroomCodeDisplay.
    public sealed class ShowroomCodeDisplay : ThemedView
    {
        private const double kCodeFontSize = 12.0;

        private readonly string m_code;
        private readonly NSButton m_copyButton;

        // ===========[ Setup/Construction/Teardown ]===========================
        public ShowroomCodeDisplay (string code)
        {
            ShowroomStyle.EnsureRegistered();
            m_code = code ?? String.Empty;
            this.BackgroundColorKey = ShowroomStyle.CodeBackground;
            this.BorderColorKey = ShowroomStyle.Divider;
            this.BorderThickness = 1.0;
            this.CornerRadius = 4.0;

            var stack = new FillStack(NSUserInterfaceLayoutOrientation.Vertical, spacing: 6.0)
            {
                Padding = new NSEdgeInsets(8, 10, 8, 10),
            };
            stack.TranslatesAutoresizingMaskIntoConstraints = false;
            this.AddSubview(stack);
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                stack.TopAnchor.ConstraintEqualTo(this.TopAnchor),
                stack.BottomAnchor.ConstraintEqualTo(this.BottomAnchor),
                stack.LeadingAnchor.ConstraintEqualTo(this.LeadingAnchor),
                stack.TrailingAnchor.ConstraintEqualTo(this.TrailingAnchor),
            });

            m_copyButton = new NSButton { Title = "Copy" };
            m_copyButton.BezelStyle = NSBezelStyle.Rounded;
            m_copyButton.Activated += this.OnCopyActivated;
            stack.Add(m_copyButton, alignment: eFillStackCrossAlignment.End);

            NSTextField codeText = NSTextField.CreateWrappingLabel(m_code);
            codeText.Font = NSFont.MonospacedSystemFont((nfloat)kCodeFontSize, NSFontWeight.Regular);
            codeText.TextColor = ThemedColors.Get(ShowroomStyle.BodyText);
            codeText.Selectable = true;
            codeText.SetContentCompressionResistancePriority(250, NSLayoutConstraintOrientation.Horizontal);
            stack.Add(codeText);
        }

        public void Teardown ()
        {
            m_copyButton.Activated -= this.OnCopyActivated;
        }

        // ===========[ Events ]================================================
        private void OnCopyActivated (object sender, EventArgs e)
        {
            NSPasteboard pasteboard = NSPasteboard.GeneralPasteboard;
            pasteboard.ClearContents();
            pasteboard.SetStringForType(m_code, NSPasteboardType.String.GetConstant());
        }
    }
}
