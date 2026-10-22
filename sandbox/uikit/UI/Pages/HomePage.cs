namespace AJutShowRoomUIKit.UI.Pages
{
    public sealed class HomePage : ShowroomPageController
    {
        public HomePage () : base("Home")
        {
        }

        protected override void BuildPage ()
        {
            this.AddHeading("AJut for UIKit");
            this.AddText("The iOS sibling of AJut.UX.WinUI3 and AJut.UX.Wpf. Each control is a UIKit view built over the same shared models in AJut.UX, so it behaves the way its XAML twin does and only the view is native.");
            this.AddText("Every page shows a control live, with the code that built it underneath. The controls default to UIKit's own system colors, so they look right in light and dark with no theme loaded; Debug > Appearance switches between the two.");
        }
    }
}
