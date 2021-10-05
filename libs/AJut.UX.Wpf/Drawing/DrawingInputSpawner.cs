namespace AJut.UX.Drawing
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows;
    using System.Windows.Input;
    using System.Windows.Media;
    using System.Windows.Shapes;


    /* TODOs
     *  - Erase mode
     *  - CTRL release, complete shape
     *  - Drawing modes, brushes, fill shape, point by point via click, etc
     *  - Drawing cursor, while in bounds & in drawing mode set cursor to none and draw circle geom filled with inner stroke
     *      this may suggest that supplying utilities to make that work externally may be better, ie:
     *          Hide cursor
     *          Provide "current location"
     *      This since we don't care about how things are drawn (up to end user)
     *  - Memory issues, maybe curve?
     *      + Configurable add point squared distance
     *      + Preview very low tolerance, goes to cursor as replace unless threshold is crossed, then goes as add
     * */
    public enum eDrawingSource
    {
        None,
        Mouse,
        Stylus,
        Touch,
    }
    public static class DrawingInputSpawner
    {
        private static readonly APUtilsRegistrationHelper APUtils = new APUtilsRegistrationHelper(typeof(DrawingInputSpawner));
        private static readonly REUtilsRegistrationHelper REUtils = new REUtilsRegistrationHelper(typeof(DrawingInputSpawner));
        private static readonly AEUtilsRegistrationHelper AEUtils = new AEUtilsRegistrationHelper(typeof(DrawingInputSpawner));


        // =====================================[ Routed Events ]=====================================

        public static RoutedEvent DrawingCreatedEvent = AEUtils.Register<RoutedEventHandler<PathGeometry>>(AddDrawingCreatedHandler, RemoveDrawingCreatedHandler);
        public static void AddDrawingCreatedHandler (DependencyObject obj, RoutedEventHandler<PathGeometry> handler)
        {
            if (obj is UIElement ui)
            {
                ui.AddHandler(DrawingCreatedEvent, handler);
            }
        }
        public static void RemoveDrawingCreatedHandler (DependencyObject obj, RoutedEventHandler<PathGeometry> handler)
        {
            if (obj is UIElement ui)
            {
                ui.RemoveHandler(DrawingCreatedEvent, handler);
            }
        }

        public static RoutedEvent EraseDrawingAlongPathEvent = AEUtils.Register<RoutedEventHandler<PathGeometry>>(AddEraseDrawingAlongPathHandler, RemoveEraseDrawingAlongPathHandler);
        public static void AddEraseDrawingAlongPathHandler (DependencyObject obj, RoutedEventHandler<PathGeometry> handler)
        {
            if (obj is UIElement ui)
            {
                ui.AddHandler(EraseDrawingAlongPathEvent, handler);
            }
        }
        public static void RemoveEraseDrawingAlongPathHandler (DependencyObject obj, RoutedEventHandler<PathGeometry> handler)
        {
            if (obj is UIElement ui)
            {
                ui.RemoveHandler(EraseDrawingAlongPathEvent, handler);
            }
        }


        // ===========================[ User Editable Attached Properties ]===========================
        public static DependencyProperty IsInDrawingModeProperty = APUtils.Register(GetIsInDrawingMode, SetIsInDrawingMode, OnIsInDrawingModeChanged);
        public static bool GetIsInDrawingMode (DependencyObject obj) => (bool)obj.GetValue(IsInDrawingModeProperty);
        public static void SetIsInDrawingMode (DependencyObject obj, bool value) => obj.SetValue(IsInDrawingModeProperty, value);

        public static DependencyProperty IsInEraseModeProperty = APUtils.Register(GetIsInEraseMode, SetIsInEraseMode);
        public static bool GetIsInEraseMode (DependencyObject obj) => (bool)obj.GetValue(IsInEraseModeProperty);
        public static void SetIsInEraseMode (DependencyObject obj, bool value) => obj.SetValue(IsInEraseModeProperty, value);

        // The number of pixels moved before a normal add point is recorded
        public static DependencyProperty SegmentSizeProperty = APUtils.Register(GetSegmentSize, SetSegmentSize, 2000.0);
        public static double GetSegmentSize (DependencyObject obj) => (double)obj.GetValue(SegmentSizeProperty);
        public static void SetSegmentSize (DependencyObject obj, double value) => obj.SetValue(SegmentSizeProperty, value);

        // The number of mouse movements before, regardless of the normal move distance, we choose to add a point anyway
        public static DependencyProperty SmallMovementThresholdProperty = APUtils.Register(GetSmallMovementThreshold, SetSmallMovementThreshold, 3);
        public static int GetSmallMovementThreshold (DependencyObject obj) => (int)obj.GetValue(SmallMovementThresholdProperty);
        public static void SetSmallMovementThreshold (DependencyObject obj, int value) => obj.SetValue(SmallMovementThresholdProperty, value);

        public static DependencyProperty StrokeWidthBaseProperty = APUtils.Register(GetStrokeWidthBase, SetStrokeWidthBase);
        public static double GetStrokeWidthBase (DependencyObject obj) => (double)obj.GetValue(StrokeWidthBaseProperty);
        public static void SetStrokeWidthBase (DependencyObject obj, double value) => obj.SetValue(StrokeWidthBaseProperty, value);

        // ===========================[ System (read only) Attached Properties ]========================
        private static DependencyPropertyKey PathInProgressPropertyKey = APUtils.RegisterReadOnly(GetPathInProgress, SetPathInProgress);
        public static DependencyProperty PathInProgressProperty = PathInProgressPropertyKey.DependencyProperty;
        public static PathBuilder GetPathInProgress (DependencyObject obj) => (PathBuilder)obj.GetValue(PathInProgressProperty);
        private static void SetPathInProgress (DependencyObject obj, PathBuilder value) => obj.SetValue(PathInProgressPropertyKey, value);

        private static DependencyPropertyKey CurrentDrawingSourcePropertyKey = APUtils.RegisterReadOnly(GetCurrentDrawingSource, SetCurrentDrawingSource);
        public static DependencyProperty CurrentDrawingSourceProperty = CurrentDrawingSourcePropertyKey.DependencyProperty;
        public static eDrawingSource GetCurrentDrawingSource (DependencyObject obj) => (eDrawingSource)obj.GetValue(CurrentDrawingSourceProperty);
        internal static void SetCurrentDrawingSource (DependencyObject obj, eDrawingSource value) => obj.SetValue(CurrentDrawingSourcePropertyKey, value);

        // =======================================[ Utilities ]=========================================
        
        // Separating this because it is unique in that it kick starts the whole process
        private static void OnIsInDrawingModeChanged (DependencyObject target, DependencyPropertyChangedEventArgs<bool> e)
        {
            if (e.HasNewValue && e.NewValue)
            {
                var ctrlTarget = (UIElement)target;

                ctrlTarget.MouseDown += Target_OnMouseDown;
                ctrlTarget.TouchDown += Target_OnTouchDown;
                ctrlTarget.StylusDown += Target_OnStylusDown;
            }
            else
            {
                var ctrlTarget = (UIElement)target;

                ctrlTarget.MouseDown -= Target_OnMouseDown;
                ctrlTarget.MouseMove -= Target_OnMouseMove;
                ctrlTarget.MouseUp -= Target_OnMouseUp;
                ctrlTarget.TouchDown -= Target_OnTouchDown;
                ctrlTarget.TouchMove -= Target_OnTouchMove;
                ctrlTarget.TouchUp -= Target_OnTouchUp;
                ctrlTarget.StylusDown -= Target_OnStylusDown;
                ctrlTarget.StylusMove -= Target_OnStylusMove;
                ctrlTarget.StylusUp -= Target_OnStyulsUp;
            }
        }

        private static void Raise (UIElement target, PathBuilder path, bool isAdd)
        {
            if (isAdd)
            {
                var drawingCreated = new RoutedEventArgs<PathGeometry>(DrawingCreatedEvent, path.Geometry);
                target.RaiseEvent(drawingCreated);
            }
            else
            {
                var drawingCreated = new RoutedEventArgs<PathGeometry>(DrawingCreatedEvent, path.Geometry);
                target.RaiseEvent(drawingCreated);
            }
        }

        private static bool CanStartDrawingFrom (UIElement sender, eDrawingSource drawingSource)
        {
            eDrawingSource currentSource = GetCurrentDrawingSource(sender);
            if (currentSource == eDrawingSource.None)
            {
                return true;
            }

            // Favor, in order, stylus, touch, then mouse - blocking the others if they exist
            switch (drawingSource)
            {
                case eDrawingSource.Mouse:  return currentSource == eDrawingSource.Mouse;
                case eDrawingSource.Touch:  return currentSource == eDrawingSource.Mouse || currentSource == eDrawingSource.Touch;
                case eDrawingSource.Stylus: return true;
                default:
                    return false;
            }
        }
        private static UIElement Start (object sender, Point start, eDrawingSource drawingSource)
        {
            var casted = (UIElement)sender;

            if (!CanStartDrawingFrom(casted, drawingSource))
            {
                return null;
            }

            SetCurrentDrawingSource(casted, drawingSource);
            SetPathInProgress(casted, new PathBuilder(start, GetSegmentSize(casted), GetSmallMovementThreshold(casted)));

            return casted;
        }
        private static void Move (object sender, Point point, eDrawingSource drawingSource)
        {
            var casted = (UIElement)sender;
            if (!CanStartDrawingFrom(casted, drawingSource))
            {
                return;
            }

            GetPathInProgress(casted).AddPoint(point);
        }
        private static UIElement Complete (object sender)
        {
            var casted = (UIElement)sender;

            casted.ReleaseMouseCapture();

            PathBuilder builder = GetPathInProgress(casted);

            SetPathInProgress(casted, null);
            SetCurrentDrawingSource(casted, eDrawingSource.None);

            Raise(casted, builder, isAdd: true);
            return casted;
        }

        // ====================================[ Event Handlers ]=======================================
        #region ========================[ Mouse ]========================
        private static void Target_OnMouseDown (object sender, MouseButtonEventArgs e)
        {
            if (!e.IsTargetPrimary())
            {
                return;
            }

            var casted = Start(sender, e.GetPosition((IInputElement)sender), eDrawingSource.Mouse);
            if (casted == null)
            {
                return;
            }

            casted.CaptureMouse();
            casted.MouseMove += Target_OnMouseMove;
            casted.MouseUp += Target_OnMouseUp;
        }

        private static void Target_OnMouseMove (object sender, MouseEventArgs e)
        {
            Move(sender, e.GetPosition((IInputElement)sender), eDrawingSource.Mouse);
        }

        private static void Target_OnMouseUp (object sender, MouseButtonEventArgs e)
        {
            UIElement casted = Complete(sender);

            casted.ReleaseMouseCapture();
            casted.MouseMove -= Target_OnMouseMove;
            casted.MouseUp -= Target_OnMouseUp;
        }
        #endregion

        #region ========================[ Touch ]========================
        private static void Target_OnTouchDown (object sender, TouchEventArgs e)
        {
            var casted = Start(sender, e.GetTouchPoint((IInputElement)sender).Position, eDrawingSource.Touch);
            if (casted == null)
            {
                return;
            }

            casted.CaptureTouch(e.TouchDevice);
            casted.TouchMove += Target_OnTouchMove;
            casted.TouchUp += Target_OnTouchUp;
        }

        private static void Target_OnTouchMove (object sender, TouchEventArgs e)
        {
            Move(sender, e.GetTouchPoint((IInputElement)sender).Position, eDrawingSource.Touch);
        }

        private static void Target_OnTouchUp (object sender, TouchEventArgs e)
        {
            UIElement casted = Complete(sender);

            casted.ReleaseTouchCapture(e.TouchDevice);
            casted.TouchMove -= Target_OnTouchMove;
            casted.TouchUp -= Target_OnTouchUp;
        }

        #endregion ========================[ Touch ]========================

        #region ========================[ Stylus ]========================
        private static void Target_OnStylusDown (object sender, StylusDownEventArgs e)
        {
            var casted = Start(sender, e.GetPosition((IInputElement)sender), eDrawingSource.Stylus);
            if (casted == null)
            {
                return;
            }

            casted.CaptureStylus();
            casted.StylusMove += Target_OnStylusMove;
            casted.StylusUp += Target_OnStyulsUp;
        }

        private static void Target_OnStylusMove (object sender, StylusEventArgs e)
        {
            Move(sender, e.GetPosition((IInputElement)sender), eDrawingSource.Stylus);
        }
        private static void Target_OnStyulsUp (object sender, StylusEventArgs e)
        {
            UIElement casted = Complete(sender);

            casted.ReleaseStylusCapture();
            casted.StylusMove -= Target_OnStylusMove;
            casted.StylusUp -= Target_OnStyulsUp;
        }

        #endregion ========================[ Stylus ]========================
    }

    public class PathBuilder
    {
        private readonly double m_addPointThresholdSquared;
        private readonly int m_verySmallMovementThreshold;
        int m_verySmallMovementCount = 0;
        PathFigure m_drawing = new PathFigure();
        Point m_last;
        public PathBuilder (Point startPoint, double addPointThreshold, int smallMovementThreshold)
        {
            m_addPointThresholdSquared = addPointThreshold * addPointThreshold;
            m_verySmallMovementThreshold = smallMovementThreshold;
            this.Geometry = new PathGeometry();
            m_drawing.StartPoint = startPoint;
            this.Geometry.Figures.Add(m_drawing);
            m_last = startPoint;
        }

        public void AddPoint (Point point)
        {
            if (m_verySmallMovementCount < m_verySmallMovementThreshold
                && m_drawing.Segments.Any()
                && (m_last - point).LengthSquared < m_addPointThresholdSquared)
            {
                ++m_verySmallMovementCount;
                m_drawing.Segments.RemoveAt(m_drawing.Segments.Count - 1);
            }
            else
            {
                m_verySmallMovementCount = 0;
                m_last = point;
            }

            m_drawing.Segments.Add(new LineSegment(point, true));
        }

        public PathGeometry Geometry { get; }
    }
}