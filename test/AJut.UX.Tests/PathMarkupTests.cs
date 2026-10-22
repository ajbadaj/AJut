namespace AJut.UX.Tests
{
    using System;
    using System.Collections.Generic;
    using AJut.UX.Helpers;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class PathMarkupTests
    {
        private const double kTolerance = 1e-9;

        // ===========[ Grammar ]===============================================
        [TestMethod]
        public void PathMarkup_EmptyMarkup_ParsesToEmptyPath ()
        {
            PathMarkupData data = PathMarkup.Parse("   ");
            Assert.AreEqual(0, data.Commands.Count);
            Assert.IsTrue(data.Bounds.IsEmpty);
            Assert.AreEqual(ePathFillRule.EvenOdd, data.FillRule);
        }

        [TestMethod]
        public void PathMarkup_FillRule_F1IsNonzeroAndF0IsEvenOdd ()
        {
            Assert.AreEqual(ePathFillRule.Nonzero, PathMarkup.Parse("F1 M0 0 L1 1").FillRule);
            Assert.AreEqual(ePathFillRule.EvenOdd, PathMarkup.Parse("F0 M0 0 L1 1").FillRule);
        }

        [TestMethod]
        public void PathMarkup_FillRuleAfterACommand_Fails ()
        {
            Assert.IsFalse(PathMarkup.TryParse("M0 0 F1 L1 1", out _, out string error));
            StringAssert.Contains(error, "character 5");
        }

        [TestMethod]
        public void PathMarkup_FirstCommandNotMove_Fails ()
        {
            Assert.IsFalse(PathMarkup.TryParse("L1 1", out PathMarkupData data, out string error));
            Assert.IsNull(data);
            StringAssert.Contains(error, "M or m");
        }

        [TestMethod]
        public void PathMarkup_MissingNumber_FailsWithOffset ()
        {
            Assert.IsFalse(PathMarkup.TryParse("M0 0 L1", out _, out string error));
            StringAssert.Contains(error, "character 7");
        }

        [TestMethod]
        public void PathMarkup_Parse_ThrowsFormatExceptionOnBadMarkup ()
        {
            Assert.ThrowsException<FormatException>(() => PathMarkup.Parse("M0 0 X1 1"));
        }

        [TestMethod]
        public void PathMarkup_CompactNumbers_SplitOnSignAndSecondDecimalPoint ()
        {
            PathMarkupData data = PathMarkup.Parse("M.5.5L-1-2");
            AssertPoint(new PathPoint(0.5, 0.5), data.Commands[0].End);
            AssertPoint(new PathPoint(-1.0, -2.0), data.Commands[1].End);
        }

        [TestMethod]
        public void PathMarkup_Exponents_AreRead ()
        {
            PathMarkupData data = PathMarkup.Parse("M1e2,2.5E-1 L-1e+1 0");
            AssertPoint(new PathPoint(100.0, 0.25), data.Commands[0].End);
            AssertPoint(new PathPoint(-10.0, 0.0), data.Commands[1].End);
        }

        [TestMethod]
        public void PathMarkup_ExtraPairsAfterMove_AreLines ()
        {
            PathMarkupData data = PathMarkup.Parse("M0 0 10 0 10 10");
            AssertKinds(data, ePathCommand.MoveTo, ePathCommand.LineTo, ePathCommand.LineTo);
            AssertPoint(new PathPoint(10.0, 10.0), data.Commands[2].End);
        }

        [TestMethod]
        public void PathMarkup_ExtraPairsAfterRelativeMove_AreRelativeLines ()
        {
            PathMarkupData data = PathMarkup.Parse("m5 5 10 0 0 10");
            AssertPoint(new PathPoint(5.0, 5.0), data.Commands[0].End);
            AssertPoint(new PathPoint(15.0, 5.0), data.Commands[1].End);
            AssertPoint(new PathPoint(15.0, 15.0), data.Commands[2].End);
        }

        // ===========[ Lowering ]==============================================
        [TestMethod]
        public void PathMarkup_HorizontalAndVertical_BecomeLines ()
        {
            PathMarkupData data = PathMarkup.Parse("M1 2 H10 V20 h-5 v-3");
            AssertKinds(data, ePathCommand.MoveTo, ePathCommand.LineTo, ePathCommand.LineTo, ePathCommand.LineTo, ePathCommand.LineTo);
            AssertPoint(new PathPoint(10.0, 2.0), data.Commands[1].End);
            AssertPoint(new PathPoint(10.0, 20.0), data.Commands[2].End);
            AssertPoint(new PathPoint(5.0, 20.0), data.Commands[3].End);
            AssertPoint(new PathPoint(5.0, 17.0), data.Commands[4].End);
        }

        [TestMethod]
        public void PathMarkup_SmoothCubic_ReflectsPreviousSecondControl ()
        {
            PathMarkupData data = PathMarkup.Parse("M0 0 C0 10 10 10 10 0 S20 -10 20 0");
            PathCommand smooth = data.Commands[2];
            Assert.AreEqual(ePathCommand.CubicTo, smooth.Kind);
            AssertPoint(new PathPoint(10.0, -10.0), smooth.Control1);
            AssertPoint(new PathPoint(20.0, -10.0), smooth.Control2);
            AssertPoint(new PathPoint(20.0, 0.0), smooth.End);
        }

        [TestMethod]
        public void PathMarkup_SmoothCubicAfterLine_UsesCurrentPoint ()
        {
            PathMarkupData data = PathMarkup.Parse("M0 0 L5 5 S10 10 15 5");
            AssertPoint(new PathPoint(5.0, 5.0), data.Commands[2].Control1);
        }

        [TestMethod]
        public void PathMarkup_Quadratic_ElevatesExactlyToCubic ()
        {
            // Quadratic from (0,0) through control (6,9) to (12,0): c1 = p0 + 2/3 (q - p0), c2 = p + 2/3 (q - p)
            PathMarkupData data = PathMarkup.Parse("M0 0 Q6 9 12 0");
            PathCommand cubic = data.Commands[1];
            AssertPoint(new PathPoint(4.0, 6.0), cubic.Control1);
            AssertPoint(new PathPoint(8.0, 6.0), cubic.Control2);
            AssertPoint(new PathPoint(12.0, 0.0), cubic.End);
        }

        [TestMethod]
        public void PathMarkup_SmoothQuadratic_ReflectsPreviousQuadraticControl ()
        {
            // The T segment's implied control is (6,9) reflected about (12,0): (18,-9)
            PathMarkupData data = PathMarkup.Parse("M0 0 Q6 9 12 0 T24 0");
            PathCommand smooth = data.Commands[2];
            AssertPoint(new PathPoint(12.0 + (2.0 / 3.0 * 6.0), -6.0), smooth.Control1);
            AssertPoint(new PathPoint(24.0 + (2.0 / 3.0 * -6.0), -6.0), smooth.Control2);
            AssertPoint(new PathPoint(24.0, 0.0), smooth.End);
        }

        [TestMethod]
        public void PathMarkup_SmoothQuadraticAfterCubic_UsesCurrentPoint ()
        {
            // With no quadratic before it, the control is the current point and the segment is straight
            PathMarkupData data = PathMarkup.Parse("M0 0 C1 1 2 1 3 0 T9 0");
            PathCommand smooth = data.Commands[2];
            AssertPoint(new PathPoint(3.0, 0.0), smooth.Control1);
            AssertPoint(new PathPoint(5.0, 0.0), smooth.Control2);
        }

        // ===========[ Closing ]===============================================
        [TestMethod]
        public void PathMarkup_CommandAfterClose_StartsAtSubpathStartWithExplicitMove ()
        {
            PathMarkupData data = PathMarkup.Parse("M10 10 L20 10 L20 20 Z l5 0");
            AssertKinds(data, ePathCommand.MoveTo, ePathCommand.LineTo, ePathCommand.LineTo, ePathCommand.Close, ePathCommand.MoveTo, ePathCommand.LineTo);
            AssertPoint(new PathPoint(10.0, 10.0), data.Commands[4].End);
            AssertPoint(new PathPoint(15.0, 10.0), data.Commands[5].End);
        }

        [TestMethod]
        public void PathMarkup_MoveAfterClose_AddsNoExtraMove ()
        {
            PathMarkupData data = PathMarkup.Parse("M0 0 L1 0 Z M5 5 L6 5");
            AssertKinds(data, ePathCommand.MoveTo, ePathCommand.LineTo, ePathCommand.Close, ePathCommand.MoveTo, ePathCommand.LineTo);
            AssertPoint(new PathPoint(5.0, 5.0), data.Commands[3].End);
        }

        [TestMethod]
        public void PathMarkup_RepeatedClose_ClosesOnce ()
        {
            PathMarkupData data = PathMarkup.Parse("M0 0 L1 0 L1 1 Z z");
            AssertKinds(data, ePathCommand.MoveTo, ePathCommand.LineTo, ePathCommand.LineTo, ePathCommand.Close);
        }

        // ===========[ Arcs ]==================================================
        [TestMethod]
        public void PathMarkup_Arc_EndsExactlyOnTarget ()
        {
            PathMarkupData data = PathMarkup.Parse("M3.3 7.1 A12.5 4.25 33 1 0 -17.75 21.9");
            PathCommand last = data.Commands[data.Commands.Count - 1];
            Assert.AreEqual(-17.75, last.End.X);
            Assert.AreEqual(21.9, last.End.Y);
        }

        [TestMethod]
        public void PathMarkup_SemicircleSweepPositive_GoesOverTheTopInTwoQuarters ()
        {
            // y-down: sweep flag 1 runs clockwise on screen, so from the left end it rises (negative y)
            PathMarkupData data = PathMarkup.Parse("M0 0 A10 10 0 0 1 20 0");
            AssertKinds(data, ePathCommand.MoveTo, ePathCommand.CubicTo, ePathCommand.CubicTo);
            AssertPoint(new PathPoint(10.0, -10.0), data.Commands[1].End, 1e-9);
            AssertBounds(new PathBounds(0.0, -10.0, 20.0, 10.0), data.Bounds, 1e-9);
        }

        [TestMethod]
        public void PathMarkup_SemicircleSweepNegative_GoesUnderTheBottom ()
        {
            PathMarkupData data = PathMarkup.Parse("M0 0 A10 10 0 0 0 20 0");
            AssertBounds(new PathBounds(0.0, 0.0, 20.0, 10.0), data.Bounds, 1e-9);
        }

        [TestMethod]
        public void PathMarkup_Arc_StaysOnTheCircleBetweenSegmentEnds ()
        {
            // A full three-quarter turn, sampled mid-segment: the cubic approximation stays within a
            // few hundredths of a percent of the radius
            const double kRadius = 50.0;
            PathMarkupData data = PathMarkup.Parse("M100 50 A50 50 0 1 1 50 0");
            var center = new PathPoint(50.0, 50.0);
            PathPoint current = data.Commands[0].End;
            for (int i = 1; i < data.Commands.Count; ++i)
            {
                PathCommand cubic = data.Commands[i];
                Assert.AreEqual(ePathCommand.CubicTo, cubic.Kind);
                foreach (double t in new[] { 0.25, 0.5, 0.75 })
                {
                    PathPoint sample = EvaluateCubic(current, cubic.Control1, cubic.Control2, cubic.End, t);
                    double distance = Math.Sqrt(Math.Pow(sample.X - center.X, 2) + Math.Pow(sample.Y - center.Y, 2));
                    Assert.AreEqual(kRadius, distance, kRadius * 0.0003);
                }

                current = cubic.End;
            }

            Assert.AreEqual(3, data.Commands.Count - 1);
        }

        [TestMethod]
        public void PathMarkup_ArcRadiiTooSmall_AreScaledUpToReach ()
        {
            // Radius 1 cannot span a 20 wide chord; scaled up it becomes the radius 10 semicircle
            PathMarkupData data = PathMarkup.Parse("M0 0 A1 1 0 0 1 20 0");
            AssertBounds(new PathBounds(0.0, -10.0, 20.0, 10.0), data.Bounds, 1e-9);
        }

        [TestMethod]
        public void PathMarkup_ArcZeroRadius_IsAStraightLine ()
        {
            PathMarkupData data = PathMarkup.Parse("M0 0 A0 5 0 0 1 10 10");
            AssertKinds(data, ePathCommand.MoveTo, ePathCommand.LineTo);
            AssertPoint(new PathPoint(10.0, 10.0), data.Commands[1].End);
        }

        [TestMethod]
        public void PathMarkup_ArcToCurrentPoint_DrawsNothing ()
        {
            PathMarkupData data = PathMarkup.Parse("M5 5 A10 10 0 0 1 5 5");
            AssertKinds(data, ePathCommand.MoveTo);
        }

        [TestMethod]
        public void PathMarkup_ArcCompactFlags_AreRead ()
        {
            PathMarkupData spaced = PathMarkup.Parse("M0 0 a10 10 0 0 1 20 0");
            PathMarkupData compact = PathMarkup.Parse("M0 0 a10 10 0 01 20 0");
            Assert.AreEqual(spaced.Commands.Count, compact.Commands.Count);
            AssertBounds(spaced.Bounds, compact.Bounds, kTolerance);
        }

        // ===========[ Bounds ]================================================
        [TestMethod]
        public void PathMarkup_Bounds_UseCurveExtremaNotControlPoints ()
        {
            // Control points reach y = 40, the curve itself peaks at 0.75 of that: 30
            PathMarkupData data = PathMarkup.Parse("M0 0 C0 40 10 40 10 0");
            AssertBounds(new PathBounds(0.0, 0.0, 10.0, 30.0), data.Bounds, 1e-9);
        }

        [TestMethod]
        public void PathMarkup_Bounds_IgnoreAMoveNothingIsDrawnFrom ()
        {
            PathMarkupData data = PathMarkup.Parse("M-100 -100 M10 10 L20 30");
            AssertBounds(new PathBounds(10.0, 10.0, 10.0, 20.0), data.Bounds, kTolerance);
        }

        [TestMethod]
        public void PathMarkup_Bounds_MoveOnlyIsEmpty ()
        {
            Assert.IsTrue(PathMarkup.Parse("M5 5").Bounds.IsEmpty);
        }

        [TestMethod]
        public void PathMarkup_Bounds_StraightLineIsFlatNotEmpty ()
        {
            PathBounds bounds = PathMarkup.Parse("M0 5 L10 5").Bounds;
            Assert.IsFalse(bounds.IsEmpty);
            Assert.AreEqual(10.0, bounds.Width, kTolerance);
            Assert.AreEqual(0.0, bounds.Height, kTolerance);
        }

        [TestMethod]
        public void PathMarkupData_CallerListChangedAfter_CommandsAndBoundsStayInStep ()
        {
            var commands = new List<PathCommand>
            {
                PathCommand.MoveTo(new PathPoint(0.0, 0.0)),
                PathCommand.LineTo(new PathPoint(10.0, 10.0)),
            };
            var data = new PathMarkupData(ePathFillRule.EvenOdd, commands);

            commands.Add(PathCommand.LineTo(new PathPoint(50.0, 50.0)));

            Assert.AreEqual(2, data.Commands.Count);
            AssertBounds(new PathBounds(0.0, 0.0, 10.0, 10.0), data.Bounds, kTolerance);
        }

        // ===========[ Fit ]===================================================
        [TestMethod]
        public void PathFit_None_IsIdentity ()
        {
            Assert.AreEqual(PathFit.Identity, PathFit.Compute(new PathBounds(10, 10, 20, 10), 100, 100, ePathStretch.None));
        }

        [TestMethod]
        public void PathFit_EmptyBounds_IsIdentity ()
        {
            Assert.AreEqual(PathFit.Identity, PathFit.Compute(PathBounds.Empty, 100, 100, ePathStretch.Uniform));
        }

        [TestMethod]
        public void PathFit_Fill_MapsBoundsCornersOntoTargetCorners ()
        {
            PathFit fit = PathFit.Compute(new PathBounds(10, 10, 20, 10), 100, 100, ePathStretch.Fill);
            AssertPoint(new PathPoint(0, 0), fit.Apply(new PathPoint(10, 10)));
            AssertPoint(new PathPoint(100, 100), fit.Apply(new PathPoint(30, 20)));
        }

        [TestMethod]
        public void PathFit_Uniform_FitsInsideAndCentersTheShortAxis ()
        {
            // 20x10 into 100x100: scale 5, so 100x50, centered vertically
            PathFit fit = PathFit.Compute(new PathBounds(10, 10, 20, 10), 100, 100, ePathStretch.Uniform);
            Assert.AreEqual(5.0, fit.ScaleX, kTolerance);
            Assert.AreEqual(5.0, fit.ScaleY, kTolerance);
            AssertPoint(new PathPoint(0, 25), fit.Apply(new PathPoint(10, 10)));
            AssertPoint(new PathPoint(100, 75), fit.Apply(new PathPoint(30, 20)));
        }

        [TestMethod]
        public void PathFit_UniformToFill_CoversAndSplitsTheOverflow ()
        {
            // 20x10 into 100x100: scale 10, so 200x100, overflowing 50 each side horizontally
            PathFit fit = PathFit.Compute(new PathBounds(10, 10, 20, 10), 100, 100, ePathStretch.UniformToFill);
            Assert.AreEqual(10.0, fit.ScaleX, kTolerance);
            AssertPoint(new PathPoint(-50, 0), fit.Apply(new PathPoint(10, 10)));
            AssertPoint(new PathPoint(150, 100), fit.Apply(new PathPoint(30, 20)));
        }

        [TestMethod]
        public void PathFit_StrokeThickness_InsetsTheTargetByHalfOnEachSide ()
        {
            PathFit fit = PathFit.Compute(new PathBounds(0, 0, 10, 10), 104, 104, ePathStretch.Fill, strokeThickness: 4);
            AssertPoint(new PathPoint(2, 2), fit.Apply(new PathPoint(0, 0)));
            AssertPoint(new PathPoint(102, 102), fit.Apply(new PathPoint(10, 10)));
        }

        [TestMethod]
        public void PathFit_UniformOnAFlatLine_ScalesByTheOtherAxisAndCentersTheFlatOne ()
        {
            PathFit fit = PathFit.Compute(new PathBounds(0, 5, 10, 0), 100, 50, ePathStretch.Uniform);
            Assert.AreEqual(10.0, fit.ScaleX, kTolerance);
            AssertPoint(new PathPoint(0, 25), fit.Apply(new PathPoint(0, 5)));
            AssertPoint(new PathPoint(100, 25), fit.Apply(new PathPoint(10, 5)));
        }

        [TestMethod]
        public void PathFit_FillOnAFlatLine_KeepsScaleOneOnTheFlatAxis ()
        {
            PathFit fit = PathFit.Compute(new PathBounds(0, 5, 10, 0), 100, 50, ePathStretch.Fill);
            Assert.AreEqual(1.0, fit.ScaleY, kTolerance);
            AssertPoint(new PathPoint(100, 25), fit.Apply(new PathPoint(10, 5)));
        }

        // ===========[ Helpers ]===============================================
        private static void AssertKinds (PathMarkupData data, params ePathCommand[] expected)
        {
            var actual = new List<ePathCommand>();
            foreach (PathCommand command in data.Commands)
            {
                actual.Add(command.Kind);
            }

            CollectionAssert.AreEqual(expected, actual);
        }

        private static void AssertPoint (PathPoint expected, PathPoint actual, double tolerance = kTolerance)
        {
            Assert.AreEqual(expected.X, actual.X, tolerance, $"X of {actual}");
            Assert.AreEqual(expected.Y, actual.Y, tolerance, $"Y of {actual}");
        }

        private static void AssertBounds (PathBounds expected, PathBounds actual, double tolerance)
        {
            Assert.AreEqual(expected.X, actual.X, tolerance, $"X of {actual}");
            Assert.AreEqual(expected.Y, actual.Y, tolerance, $"Y of {actual}");
            Assert.AreEqual(expected.Width, actual.Width, tolerance, $"Width of {actual}");
            Assert.AreEqual(expected.Height, actual.Height, tolerance, $"Height of {actual}");
        }

        private static PathPoint EvaluateCubic (PathPoint p0, PathPoint p1, PathPoint p2, PathPoint p3, double t)
        {
            double mt = 1.0 - t;
            return new PathPoint(
                (mt * mt * mt * p0.X) + (3 * mt * mt * t * p1.X) + (3 * mt * t * t * p2.X) + (t * t * t * p3.X),
                (mt * mt * mt * p0.Y) + (3 * mt * mt * t * p1.Y) + (3 * mt * t * t * p2.Y) + (t * t * t * p3.Y)
            );
        }
    }
}
