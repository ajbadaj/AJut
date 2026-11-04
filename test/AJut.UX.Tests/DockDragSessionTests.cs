#nullable enable

namespace AJut.UX.Tests
{
    using System.Collections.Generic;
    using System.Globalization;
    using AJut.UX.Docking;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class DockDragSessionTests
    {
        // ===[ Targets and widgets ]===

        [TestMethod]
        public void DDS_ZoneUnderThePointer_ShowsItsOverlay_AndDims ()
        {
            var host = new TestHost();
            var session = new DockDragSession<Zone, Widget, Window>(host);
            var zone = new Zone("A");

            session.Update(zone, null, null);

            CollectionAssert.AreEqual(new[] { "show A", "dim" }, host.Calls);
            Assert.AreSame(zone, session.DropTarget);
            Assert.IsNull(session.EngagedWidget);
        }

        [TestMethod]
        public void DDS_MovingBetweenZonesAndWidgets_SwitchesWhatShows ()
        {
            var host = new TestHost();
            var session = new DockDragSession<Zone, Widget, Window>(host);
            var a = new Zone("A");
            var b = new Zone("B");
            var left = new Widget("A-left");

            session.Update(a, null, null);
            session.Update(a, left, null);
            session.Update(b, null, null);
            session.Update(null, null, null);

            CollectionAssert.AreEqual(
                new[] { "show A", "dim", "engage A-left", "disengage A-left", "hide A", "show B", "hide B", "undim" },
                host.Calls
            );
        }

        [TestMethod]
        public void DDS_NothingChanged_CallsNothing ()
        {
            var host = new TestHost();
            var session = new DockDragSession<Zone, Widget, Window>(host);
            var a = new Zone("A");
            var widget = new Widget("A-top");

            session.Update(a, widget, null);
            host.Calls.Clear();
            session.Update(a, widget, null);

            Assert.AreEqual(0, host.Calls.Count);
        }

        // ===[ Resting on a window ]===

        [TestMethod]
        public void DDS_RestingOnAWindow_FlashesThenRaises ()
        {
            var host = new TestHost();
            var session = new DockDragSession<Zone, Widget, Window>(host);
            var back = new Window("Back");

            // The first tick only notes the window; counting starts on the next
            Tick(session, back, 1 + DockDragSession<Zone, Widget, Window>.kArmingStartTicks - 1);
            Assert.AreEqual(0, host.Calls.Count, "Nothing before the arming tick");

            Tick(session, back, 1);
            Assert.AreEqual(2, host.Calls.Count, "The arming tick begins the flash and sets its first opacity: " + string.Join("; ", host.Calls));
            Assert.AreEqual("begin flash Back", host.Calls[0]);
            string flashAtStart = host.Calls[1];
            StringAssert.StartsWith(flashAtStart, "flash Back ");

            host.Calls.Clear();
            Tick(session, back, DockDragSession<Zone, Widget, Window>.kFlashPeriodTicks);
            string flashAfterAPeriod = host.Calls[host.Calls.Count - 1];
            StringAssert.StartsWith(flashAfterAPeriod, "flash Back ");
            Assert.AreNotEqual(flashAtStart, flashAfterAPeriod, "One flash period on, the flash has changed");

            host.Calls.Clear();
            int toRaise = DockDragSession<Zone, Widget, Window>.kBringForwardTicks
                - DockDragSession<Zone, Widget, Window>.kArmingStartTicks
                - DockDragSession<Zone, Widget, Window>.kFlashPeriodTicks;
            Tick(session, back, toRaise);
            CollectionAssert.AreEqual(new[] { "end flash Back", "raise Back" }, host.Calls.GetRange(host.Calls.Count - 2, 2));
        }

        [TestMethod]
        public void DDS_RestingAfterARaise_CountsAgainFromZero ()
        {
            var host = new TestHost();
            var session = new DockDragSession<Zone, Widget, Window>(host);
            var back = new Window("Back");

            Tick(session, back, 1 + DockDragSession<Zone, Widget, Window>.kBringForwardTicks);
            host.Calls.Clear();

            Tick(session, back, DockDragSession<Zone, Widget, Window>.kArmingStartTicks - 1);
            Assert.AreEqual(0, host.Calls.Count);
            Tick(session, back, 1);
            Assert.AreEqual("begin flash Back", host.Calls[0]);
        }

        [TestMethod]
        public void DDS_LeavingAFlashingWindow_EndsTheFlash_AndRestarts ()
        {
            var host = new TestHost();
            var session = new DockDragSession<Zone, Widget, Window>(host);
            var back = new Window("Back");
            var other = new Window("Other");

            Tick(session, back, 1 + DockDragSession<Zone, Widget, Window>.kArmingStartTicks + 3);
            host.Calls.Clear();

            session.Update(null, null, other);
            CollectionAssert.AreEqual(new[] { "end flash Back" }, host.Calls);

            host.Calls.Clear();
            Tick(session, other, DockDragSession<Zone, Widget, Window>.kArmingStartTicks - 1);
            Assert.AreEqual(0, host.Calls.Count, "The count restarted for the new window");
        }

        // ===[ Ending ]===

        [TestMethod]
        public void DDS_End_PutsEverythingBack_OnceOnly ()
        {
            var host = new TestHost();
            var session = new DockDragSession<Zone, Widget, Window>(host);
            var zone = new Zone("A");
            var widget = new Widget("A-right");
            var back = new Window("Back");

            session.Update(zone, widget, back);
            Tick(session, back, DockDragSession<Zone, Widget, Window>.kArmingStartTicks, zone, widget);
            host.Calls.Clear();

            Assert.AreSame(widget, session.EngagedWidget, "Read before End: what a drop uses");
            session.End();
            session.End();

            CollectionAssert.AreEqual(new[] { "undim", "end flash Back", "disengage A-right", "hide A" }, host.Calls);
            Assert.IsTrue(session.IsEnded);
        }

        [TestMethod]
        public void DDS_AfterEnd_UpdatesAreIgnored ()
        {
            var host = new TestHost();
            var session = new DockDragSession<Zone, Widget, Window>(host);
            session.End();

            session.Update(new Zone("A"), null, null);

            Assert.AreEqual(0, host.Calls.Count);
        }

        // ===[ Helpers ]===

        private static void Tick (DockDragSession<Zone, Widget, Window> session, Window hover, int count, Zone? zone = null, Widget? widget = null)
        {
            for (int i = 0; i < count; ++i)
            {
                session.Update(zone, widget, hover);
            }
        }

        private sealed record Zone (string Name);
        private sealed record Widget (string Name);
        private sealed record Window (string Name);

        private sealed class TestHost : IDockDragSessionHost<Zone, Widget, Window>
        {
            public List<string> Calls { get; } = new List<string>();

            public void ShowDropTarget (Zone zone, bool isTarget) => this.Calls.Add($"{(isTarget ? "show" : "hide")} {zone.Name}");
            public void EngageWidget (Widget widget, bool isEngaged) => this.Calls.Add($"{(isEngaged ? "engage" : "disengage")} {widget.Name}");
            public void DimDragSource (bool isDimmed) => this.Calls.Add(isDimmed ? "dim" : "undim");
            public void BeginHoverFlash (Window window) => this.Calls.Add($"begin flash {window.Name}");
            public void SetHoverFlashOpacity (Window window, double opacity) => this.Calls.Add($"flash {window.Name} {opacity.ToString(CultureInfo.InvariantCulture)}");
            public void EndHoverFlash (Window window) => this.Calls.Add($"end flash {window.Name}");
            public void RaiseHoverWindow (Window window) => this.Calls.Add($"raise {window.Name}");
        }
    }
}
