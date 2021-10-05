namespace AJut.UX.AttachedProperties
{
    using System;
    using System.Collections.Generic;
    using System.Drawing.Imaging;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Reflection;
    using System.Windows;
    using System.Windows.Media.Imaging;
    using System.Windows.Threading;
    using AJut.IO;
    using ImageControl = System.Windows.Controls.Image;
    using ImageStorage = System.Drawing.Image;

    public static class Gif
    {
        // =============[ Fields ]================
        private static readonly APUtilsRegistrationHelper APUtils = new APUtilsRegistrationHelper(typeof(Gif));

        // =============[ Properties User Changes ]================
        //public static DependencyProperty PathProperty = APUtils.Register(GetPath, SetPath, null, HandlePathChanged, CoerceUtils.CallbackForUri);
        public static DependencyProperty PathProperty = APUtils.Register(GetPath, SetPath, HandlePathChanged);
        public static Uri GetPath (DependencyObject obj) => (Uri)obj.GetValue(PathProperty);
        public static void SetPath (DependencyObject obj, Uri value) => obj.SetValue(PathProperty, value);

        public static DependencyProperty IsPlayingProperty = APUtils.Register(GetIsPlaying, SetIsPlaying, HandleIsPlayingChanged);
        public static bool GetIsPlaying (DependencyObject obj) => (bool)obj.GetValue(IsPlayingProperty);
        public static void SetIsPlaying (DependencyObject obj, bool value) => obj.SetValue(IsPlayingProperty, value);

        public static DependencyProperty OverrideIsLoopingProperty = APUtils.Register(GetOverrideIsLooping, SetOverrideIsLooping);
        public static bool GetOverrideIsLooping (DependencyObject obj) => (bool)obj.GetValue(OverrideIsLoopingProperty);
        public static void SetOverrideIsLooping (DependencyObject obj, bool value) => obj.SetValue(OverrideIsLoopingProperty, value);

        public static DependencyProperty OverrideFrameDelayMSProperty = APUtils.Register(GetOverrideFrameDelayMS, SetOverrideFrameDelayMS);
        public static int GetOverrideFrameDelayMS (DependencyObject obj) => (int)obj.GetValue(OverrideFrameDelayMSProperty);
        public static void SetOverrideFrameDelayMS (DependencyObject obj, int value) => obj.SetValue(OverrideFrameDelayMSProperty, value);

        public static DependencyProperty ShowTransparencyProperty = APUtils.Register(GetShowTransparency, SetShowTransparency);
        public static bool GetShowTransparency (DependencyObject obj) => (bool)obj.GetValue(ShowTransparencyProperty);
        public static void SetShowTransparency (DependencyObject obj, bool value) => obj.SetValue(ShowTransparencyProperty, value);

        public static DependencyProperty CurrentFrameIndexProperty = APUtils.Register(GetCurrentFrameIndex, SetCurrentFrameIndex, HandleCurrentFrameIndexChanged);
        public static int GetCurrentFrameIndex (DependencyObject obj) => (int)obj.GetValue(CurrentFrameIndexProperty);
        public static void SetCurrentFrameIndex (DependencyObject obj, int value) => obj.SetValue(CurrentFrameIndexProperty, value);

        // =============[ Read Only Properties ]================
        private static DependencyPropertyKey InfoPropertyKey = APUtils.RegisterReadOnly(GetInfo, SetInfo);
        public static DependencyProperty InfoProperty = InfoPropertyKey.DependencyProperty;
        public static GifInfoCache GetInfo (DependencyObject obj) => (GifInfoCache)obj.GetValue(InfoProperty);
        internal static void SetInfo (DependencyObject obj, GifInfoCache value) => obj.SetValue(InfoPropertyKey, value);

        private static DependencyPropertyKey FrameUpdateTimerPropertyKey = APUtils.RegisterReadOnly(GetFrameUpdateTimer, SetFrameUpdateTimer, HandleFrameUpdateTimerChanged);
        public static DependencyProperty FrameUpdateTimerProperty = FrameUpdateTimerPropertyKey.DependencyProperty;
        public static DispatcherTimer GetFrameUpdateTimer (DependencyObject obj) => (DispatcherTimer)obj.GetValue(FrameUpdateTimerProperty);
        internal static void SetFrameUpdateTimer (DependencyObject obj, DispatcherTimer value) => obj.SetValue(FrameUpdateTimerPropertyKey, value);

        // =============[ Private Update Handlers ]================
        private static void HandlePathChanged (DependencyObject d, DependencyPropertyChangedEventArgs<Uri> e)
        {
            if (!(d is ImageControl) || e.HasOldValue)
            {
                ClearGifInfo(d);
            }

            if (!e.HasNewValue)
            {
                return;
            }

            Stream imageStream = null;
            try
            {
                ImageStorage image = null;
                if (!e.NewValue.IsAbsoluteUri || e.NewValue.IsFile)
                {
                    image = ImageStorage.FromFile(e.NewValue.OriginalString);
                }
                else if (e.NewValue.Scheme.Equals("pack", StringComparison.InvariantCultureIgnoreCase))
                {

                    int stopInd = e.NewValue.AbsolutePath.IndexOf(';');
                    string assemblyName = e.NewValue.AbsolutePath.Substring(0, stopInd).Trim('/');
                    Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == assemblyName);
                    string embeddedResourcePath = e.NewValue.AbsolutePath.Replace($"{assemblyName};component/", "").Trim('/');
                    embeddedResourcePath = FileHelpers.GenerateEmbeddedResourceName(embeddedResourcePath, assembly);

                    imageStream = assembly.GetManifestResourceStream(embeddedResourcePath);
                    if (imageStream != null)
                    {
                        image = ImageStorage.FromStream(imageStream);
                    }
                }
                else
                {
                    using (HttpWebResponse response = (HttpWebResponse)WebRequest.Create(e.NewValue).GetResponse())
                    {
                        using (BinaryReader reader = new BinaryReader(response.GetResponseStream()))
                        {
                            imageStream = new MemoryStream();
                            byte[] transferBuffer = reader.ReadBytes(1024);
                            while (transferBuffer.Length > 0)
                            {
                                imageStream.Write(transferBuffer, 0, transferBuffer.Length);
                                transferBuffer = reader.ReadBytes(1024);
                            }

                            image = ImageStorage.FromStream(imageStream);
                        }
                    }
                }

                SetCurrentFrameIndex(d, -1);
                SetInfo(d, new GifInfoCache(image));
                SetCurrentFrameIndex(d, 0);
                ResetTimer(d);
            }
            catch (Exception exception)
            {
                Logger.LogError(exception);
                ClearGifInfo(d);
            }
            finally
            {
                if (imageStream != null)
                {
                    imageStream.Close();
                    imageStream.Dispose();
                    imageStream = null;
                }
            }
        }

        private static void HandleFrameUpdateTimerChanged (DependencyObject d, DependencyPropertyChangedEventArgs<DispatcherTimer> e)
        {
            var timer = GetFrameUpdateTimer(d);
            if (e.OldValue != null)
            {
                e.OldValue.Stop();
                e.OldValue.Tick -= _OnUpdateTimerTick;
            }
            if (e.NewValue != null)
            {
                e.NewValue.Tick -= _OnUpdateTimerTick;
                e.NewValue.Tick += _OnUpdateTimerTick;
            }

            void _OnUpdateTimerTick (object _sender, EventArgs _e)
            {
                SetCurrentFrameIndex(d, GetCurrentFrameIndex(d) + 1);
                ResetTimer(d, start: true);
            }
        }

        private static void HandleCurrentFrameIndexChanged (DependencyObject d, DependencyPropertyChangedEventArgs<int> e)
        {
            GifInfoCache info = GetInfo(d);
            if (info == null)
            {
                return;
            }

            if (e.NewValue >= info.Frames.Count)
            {
                if (IsLooping(d, info))
                {
                    SetCurrentFrameIndex(d, 0);
                    return;
                }
                else
                {
                    SetCurrentFrameIndex(d, info.Frames.Count - 1);
                    SetIsPlaying(d, false);
                    return;
                }
            }

            if (e.NewValue >= 0 && d is ImageControl imageControl)
            {
                imageControl.Source = info.Frames[e.NewValue].ImageSource;
            }
        }

        private static void HandleIsPlayingChanged (DependencyObject d, DependencyPropertyChangedEventArgs<bool> e)
        {
            // Is it now being asked to play again?
            if (e.NewValue)
            {
                var info = GetInfo(d);
                if (info == null)
                {
                    return;
                }

                // If we got to the end and we are showing a non-looping gif, then restart
                if (!IsLooping(d, info) && GetCurrentFrameIndex(d) >= info.Frames.Count - 1)
                {
                    SetCurrentFrameIndex(d, 0);
                }
            }

            ResetTimer(d, e.NewValue);
        }

        // ==================[ Utilities ]========================
        private static bool IsLooping (DependencyObject d, GifInfoCache info) => d.IsSetLocally(OverrideIsLoopingProperty) ? GetOverrideIsLooping(d) : info.IsLooping;
        private static void ClearGifInfo (DependencyObject d)
        {
            SetIsPlaying(d, false);
            SetFrameUpdateTimer(d, null);
            SetInfo(d, null);
        }

        private static void ResetTimer (DependencyObject d, bool start = false)
        {
            var currentTimer = GetFrameUpdateTimer(d);
            currentTimer?.Stop();

            var info = GetInfo(d);
            if (info == null)
            {
                return;
            }

            int frameIndex = GetCurrentFrameIndex(d);
            var newInterval = TimeSpan.FromMilliseconds(
                d.IsSetLocally(OverrideFrameDelayMSProperty)
                ? GetOverrideFrameDelayMS(d)
                : info.Frames[frameIndex].DelayToNextMS
            );

            if (currentTimer == null)
            {
                currentTimer = new DispatcherTimer(DispatcherPriority.Normal, d.Dispatcher);
                SetFrameUpdateTimer(d, currentTimer);
            }

            if (currentTimer.Interval != newInterval)
            {
                currentTimer.Interval = newInterval;
            }

            if (start)
            {
                currentTimer.Start();
            }
        }
    }

    // TODO: Consider making this it's own gif library, some possibility of read\write apps later would be nice
    public class GifInfoCache
    {
        private const int kIsLooping = 20737;

        public List<GifFrame> Frames { get; }
        public bool IsLooping { get; }
        public int SourcePixelWidth { get; }
        public int SourcePixelHeight { get; }

        public GifInfoCache (ImageStorage image)
        {
            var frameDimension = new FrameDimension(image.FrameDimensionsList[0]);
            image.SelectActiveFrame(frameDimension, 0);

            this.IsLooping = BitConverter.ToInt16(image.GetPropertyItem(kIsLooping).Value, 0) != 1;
            this.SourcePixelWidth = image.Width;
            this.SourcePixelHeight = image.Height;

            int frameCount = image.GetFrameCount(frameDimension);
            this.Frames = new List<GifFrame>(frameCount);
            for (int frameIndex = 0; frameIndex < frameCount; ++frameIndex)
            {
                image.SelectActiveFrame(frameDimension, frameIndex);
                this.Frames.Add(new GifFrame(this, image, frameIndex));
            }
        }
    }
    public class GifFrame
    {
        private const int kFrameDelayProperty = 20736;

        public BitmapImage ImageSource { get; set; }
        public int DelayToNextMS { get; set; }
        public int FrameIndex { get; set; }

        public GifFrame (GifInfoCache info, ImageStorage image, int frameIndex)
        {
            this.ImageSource = ImageUtils.GetImageSourceFrom(image);
            this.DelayToNextMS = BitConverter.ToInt32(image.GetPropertyItem(kFrameDelayProperty).Value, frameIndex) * 10;
            if (this.DelayToNextMS == 0 && info.Frames.Count > 0)
            {
                this.DelayToNextMS = info.Frames[0].DelayToNextMS;
            }
            else
            {
                this.DelayToNextMS = 100;
            }

            this.FrameIndex = frameIndex;
        }
    }
}
