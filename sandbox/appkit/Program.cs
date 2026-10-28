namespace AJutShowRoomAppKit
{
    using AppKit;

    public static class Program
    {
        public static void Main (string[] args)
        {
            NSApplication.Init();
            NSApplication.SharedApplication.Delegate = new AppDelegate();
            NSApplication.Main(args);
        }
    }
}
