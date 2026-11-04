namespace AJut.UX.Controls
{
    using System;

    /// <summary>
    /// What a BumpStack's bump buttons show and do, toolkit free: the buttons show only while there is somewhere to
    /// scroll, each one is enabled while the stack can still go its way, and a bump never goes past either end. The
    /// rules are WinUI3's BumpStack's. Offsets and lengths are measured along the stack: widths and x for a horizontal
    /// stack, heights and y for a vertical one.
    /// </summary>
    /// <param name="AreShown">Whether the bump buttons show at all: scrolling is on and the content runs past the stack</param>
    /// <param name="CanBumpBack">Whether the leading button (left, or top) is enabled: the stack is not at its start</param>
    /// <param name="CanBumpForward">Whether the trailing button (right, or bottom) is enabled: the stack is not at its end</param>
    public readonly record struct BumpStackRules (bool AreShown, bool CanBumpBack, bool CanBumpForward)
    {
        /// <summary>
        /// How close to an end counts as at it, and how little overflow counts as none, so a fraction left over from layout
        /// rounding neither shows the buttons nor keeps one enabled at an end the stack is already at
        /// </summary>
        public const double kEndTolerance = 0.5;

        /// <summary>
        /// How far a line based mouse wheel scrolls per line it reports, in points. A trackpad reports points itself.
        /// </summary>
        public const double kWheelLineStep = 16.0;

        /// <summary>The bump buttons for where the stack is scrolled to</summary>
        /// <param name="scrollingEnabled">Whether the stack scrolls at all, or only clips its content</param>
        /// <param name="offset">How far the stack is scrolled from its start</param>
        /// <param name="scrollableLength">How far it can scroll: the content's length less the stack's</param>
        public static BumpStackRules For (bool scrollingEnabled, double offset, double scrollableLength)
        {
            bool areShown = scrollingEnabled && (scrollableLength > kEndTolerance);
            return new BumpStackRules(
                AreShown: areShown,
                CanBumpBack: areShown && (offset > kEndTolerance),
                CanBumpForward: areShown && (offset < (scrollableLength - kEndTolerance))
            );
        }

        /// <summary>Where a bump of <paramref name="delta"/> from <paramref name="offset"/> lands, kept inside the scrollable range</summary>
        public static double NudgedOffset (double offset, double delta, double scrollableLength)
        {
            return Math.Max(0.0, Math.Min(Math.Max(0.0, scrollableLength), offset + delta));
        }

        /// <summary>
        /// The space kept clear at each end of the content while the buttons show, so scrolling all the way to an end parks
        /// its button over empty space rather than over the content: the button's own length plus the extra clearance
        /// asked for. Nothing is kept clear while the buttons are hidden.
        /// </summary>
        public static double ButtonClearance (bool areShown, double buttonLength, double edgeClearanceOffset)
        {
            return areShown ? (buttonLength + edgeClearanceOffset) : 0.0;
        }

        /// <summary>
        /// What a scroll wheel or trackpad does to the stack, for a toolkit whose own scrolling follows the wheel's axis. A
        /// plain mouse wheel only scrolls vertically, so on a horizontal stack a mostly vertical scroll is turned into a
        /// sideways one, wheel down moving toward the end, and the offset change is given. Anything else (a mostly
        /// sideways swipe, or any scroll on a vertical stack) returns false and is left to the toolkit's own scrolling.
        /// </summary>
        /// <param name="isHorizontal">Whether the stack runs left to right</param>
        /// <param name="deltaX">The scroll's sideways amount, in lines or points</param>
        /// <param name="deltaY">The scroll's up and down amount, in lines or points, positive for scrolling up</param>
        /// <param name="isInPoints">Whether the amounts are points (a trackpad) rather than lines (a line based wheel)</param>
        /// <param name="offsetDelta">How far to move the stack's offset along it</param>
        public static bool TryTurnWheelSideways (bool isHorizontal, double deltaX, double deltaY, bool isInPoints, out double offsetDelta)
        {
            offsetDelta = 0.0;
            if (!isHorizontal || (Math.Abs(deltaY) <= Math.Abs(deltaX)))
            {
                return false;
            }

            offsetDelta = -(isInPoints ? deltaY : deltaY * kWheelLineStep);
            return true;
        }
    }
}
