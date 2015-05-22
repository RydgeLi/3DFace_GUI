// --------------------------------------------------------------------------------------------------------------------
// <copyright file="FaceTrackingViewer.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace FaceTrackingBasics
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using Microsoft.Kinect;
    using Microsoft.Kinect.Toolkit.FaceTracking;

    using Point = System.Windows.Point;
    //
    using Rect = Microsoft.Kinect.Toolkit.FaceTracking.Rect;
    //

    /// <summary>
    /// Class that uses the Face Tracking SDK to display a face mask for
    /// tracked skeletons
    /// </summary>
    public partial class FaceTrackingViewer : UserControl, IDisposable
    {
        public static readonly DependencyProperty KinectProperty = DependencyProperty.Register(
            "Kinect", 
            typeof(KinectSensor), 
            typeof(FaceTrackingViewer), 
            new PropertyMetadata(
                null, (o, args) => ((FaceTrackingViewer)o).OnSensorChanged((KinectSensor)args.OldValue, (KinectSensor)args.NewValue)));

        //
        private static readonly int Bgr32BytesPerPixel = (PixelFormats.Bgr32.BitsPerPixel + 7) / 8;
        private const int FaceRectOffset = 10;
        //
        private const uint MaxMissedFrames = 100;

        private readonly Dictionary<int, SkeletonFaceTracker> trackedSkeletons = new Dictionary<int, SkeletonFaceTracker>();

        private byte[] colorImage;

        private ColorImageFormat colorImageFormat = ColorImageFormat.Undefined;

        private short[] depthImage;

        private DepthImageFormat depthImageFormat = DepthImageFormat.Undefined;

        private bool disposed;

        private Skeleton[] skeletonData;

        public FaceTrackingViewer()
        {
            this.InitializeComponent();
        }

        ~FaceTrackingViewer()
        {
            this.Dispose(false);
        }

        public KinectSensor Kinect
        {
            get
            {
                return (KinectSensor)this.GetValue(KinectProperty);
            }

            set
            {
                this.SetValue(KinectProperty, value);
            }
        }

        //
        public int WriteFaceInfo(string pathRoot, string probe_name)
        {
            int faceCount = 0;
            foreach (var tracker in this.trackedSkeletons)
            {
                if (!tracker.Value.LastFaceTrackSucceeded)
                {
                    continue;
                }

                // 保存脸部区域图像和深度值
                var faceRect = new Rect(tracker.Value.FaceRect.Left - FaceRectOffset, tracker.Value.FaceRect.Top - FaceRectOffset,
                    tracker.Value.FaceRect.Right + FaceRectOffset, tracker.Value.FaceRect.Bottom);

                var faceWriteableBitmap = new WriteableBitmap(faceRect.Width, faceRect.Height, 96, 96, PixelFormats.Bgr32, null);

                faceWriteableBitmap.WritePixels(
                        new Int32Rect(0, 0, faceRect.Width, faceRect.Height), this.colorImage,
                        640 * Bgr32BytesPerPixel, (faceRect.Top * 640 + faceRect.Left) * 4);

                BitmapEncoder bitmapEncoder = new BmpBitmapEncoder();
                bitmapEncoder.Frames.Add(BitmapFrame.Create(faceWriteableBitmap));

                string imagePath = pathRoot + "\\" + probe_name + ".bmp";
                //string depthPath = pathRoot + "\\face_depth_" + probe_name + ".txt";
                try
                {
                    using (var fs = new FileStream(imagePath, FileMode.Create))
                    {
                        bitmapEncoder.Save(fs);
                    }

                    //using (var sw = new StreamWriter(depthPath))
                    //{
                    //    sw.WriteLine("{0} {1} {2} {3}", faceRect.Left, faceRect.Top, faceRect.Width, faceRect.Height);

                    //    for (int i = 0; i < faceRect.Height; i++)
                    //    {
                    //        for (int j = 0; j < faceRect.Width; j++)
                    //        {
                    //            var depth = (this.depthImage[(faceRect.Top + i) * 640 + faceRect.Left + j]) >> 3;
                    //            sw.WriteLine("{0, -4} {1, -4} {2}", j, i, depth);
                    //        }
                    //    }
                    //}
                }
                catch (IOException)
                {

                }

                // 写入脸部特征点信息
                try
                {
                    string landmarkPath = pathRoot + "\\" + probe_name + ".txt";
                    var fs = new FileStream(landmarkPath, FileMode.Create);
                    var sw = new StreamWriter(fs);

                    // 先写入6个特征点用以对齐，依次为
                    // 21: 左眼中心
                    // 54: 右眼中心
                    // 5 : 鼻尖
                    // 88: 左嘴角
                    // 89: 右嘴角
                    // 10: 下巴底部

                    var facePoints = tracker.Value.FacePoints;
                    var face3DPoints = tracker.Value.Face3DPoints;

                    sw.WriteLine("{0, -8}  {1, -8}  {2}", facePoints[21].X, facePoints[21].Y, face3DPoints[21].Z * 1000);
                    sw.WriteLine("{0, -8}  {1, -8}  {2}", facePoints[54].X, facePoints[54].Y, face3DPoints[54].Z * 1000);
                    sw.WriteLine("{0, -8}  {1, -8}  {2}", facePoints[5].X, facePoints[5].Y, face3DPoints[5].Z * 1000);
                    sw.WriteLine("{0, -8}  {1, -8}  {2}", facePoints[88].X, facePoints[88].Y, face3DPoints[88].Z * 1000);
                    sw.WriteLine("{0, -8}  {1, -8}  {2}", facePoints[89].X, facePoints[89].Y, face3DPoints[89].Z * 1000);
                    sw.WriteLine("{0, -8}  {1, -8}  {2}", facePoints[10].X, facePoints[10].Y, face3DPoints[10].Z * 1000);

                    for (int i = 0; i < facePoints.Count; ++i)
                    {
                        if (i != 21 && i != 54 && i != 5 && i != 88 && i != 89 && i != 10)
                        {
                            sw.WriteLine("{0, -8}  {1, -8}  {2}", facePoints[i].X, facePoints[i].Y, face3DPoints[i].Z * 1000);
                        }
                    }

                    sw.Flush();
                    sw.Close();
                    fs.Close();
                }
                catch (Exception)
                {

                }

                ++faceCount;
            }

            return faceCount;
        }
        //
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                this.ResetFaceTracking();

                this.disposed = true;
            }
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            foreach (SkeletonFaceTracker faceInformation in this.trackedSkeletons.Values)
            {
                faceInformation.DrawFaceModel(drawingContext);
            }
        }

        private void OnAllFramesReady(object sender, AllFramesReadyEventArgs allFramesReadyEventArgs)
        {
            ColorImageFrame colorImageFrame = null;
            DepthImageFrame depthImageFrame = null;
            SkeletonFrame skeletonFrame = null;

            try
            {
                colorImageFrame = allFramesReadyEventArgs.OpenColorImageFrame();
                depthImageFrame = allFramesReadyEventArgs.OpenDepthImageFrame();
                skeletonFrame = allFramesReadyEventArgs.OpenSkeletonFrame();

                if (colorImageFrame == null || depthImageFrame == null || skeletonFrame == null)
                {
                    return;
                }

                // Check for image format changes.  The FaceTracker doesn't
                // deal with that so we need to reset.
                if (this.depthImageFormat != depthImageFrame.Format)
                {
                    this.ResetFaceTracking();
                    this.depthImage = null;
                    this.depthImageFormat = depthImageFrame.Format;
                }

                if (this.colorImageFormat != colorImageFrame.Format)
                {
                    this.ResetFaceTracking();
                    this.colorImage = null;
                    this.colorImageFormat = colorImageFrame.Format;
                }

                // Create any buffers to store copies of the data we work with
                if (this.depthImage == null)
                {
                    this.depthImage = new short[depthImageFrame.PixelDataLength];
                }

                if (this.colorImage == null)
                {
                    this.colorImage = new byte[colorImageFrame.PixelDataLength];
                }
                
                // Get the skeleton information
                if (this.skeletonData == null || this.skeletonData.Length != skeletonFrame.SkeletonArrayLength)
                {
                    this.skeletonData = new Skeleton[skeletonFrame.SkeletonArrayLength];
                }

                colorImageFrame.CopyPixelDataTo(this.colorImage);
                depthImageFrame.CopyPixelDataTo(this.depthImage);
                skeletonFrame.CopySkeletonDataTo(this.skeletonData);

                // Update the list of trackers and the trackers with the current frame information
                foreach (Skeleton skeleton in this.skeletonData)
                {
                    if (skeleton.TrackingState == SkeletonTrackingState.Tracked
                        || skeleton.TrackingState == SkeletonTrackingState.PositionOnly)
                    {
                        // We want keep a record of any skeleton, tracked or untracked.
                        if (!this.trackedSkeletons.ContainsKey(skeleton.TrackingId))
                        {
                            this.trackedSkeletons.Add(skeleton.TrackingId, new SkeletonFaceTracker());
                        }

                        // Give each tracker the upated frame.
                        SkeletonFaceTracker skeletonFaceTracker;
                        if (this.trackedSkeletons.TryGetValue(skeleton.TrackingId, out skeletonFaceTracker))
                        {
                            skeletonFaceTracker.OnFrameReady(this.Kinect, colorImageFormat, colorImage, depthImageFormat, depthImage, skeleton);
                            skeletonFaceTracker.LastTrackedFrame = skeletonFrame.FrameNumber;
                        }
                    }
                }

                this.RemoveOldTrackers(skeletonFrame.FrameNumber);

                this.InvalidateVisual();
            }
            finally
            {
                if (colorImageFrame != null)
                {
                    colorImageFrame.Dispose();
                }

                if (depthImageFrame != null)
                {
                    depthImageFrame.Dispose();
                }

                if (skeletonFrame != null)
                {
                    skeletonFrame.Dispose();
                }
            }
        }

        private void OnSensorChanged(KinectSensor oldSensor, KinectSensor newSensor)
        {
            if (oldSensor != null)
            {
                oldSensor.AllFramesReady -= this.OnAllFramesReady;
                this.ResetFaceTracking();
            }

            if (newSensor != null)
            {
                newSensor.AllFramesReady += this.OnAllFramesReady;
            }
        }

        /// <summary>
        /// Clear out any trackers for skeletons we haven't heard from for a while
        /// </summary>
        private void RemoveOldTrackers(int currentFrameNumber)
        {
            var trackersToRemove = new List<int>();

            foreach (var tracker in this.trackedSkeletons)
            {
                uint missedFrames = (uint)currentFrameNumber - (uint)tracker.Value.LastTrackedFrame;
                if (missedFrames > MaxMissedFrames)
                {
                    // There have been too many frames since we last saw this skeleton
                    trackersToRemove.Add(tracker.Key);
                }
            }

            foreach (int trackingId in trackersToRemove)
            {
                this.RemoveTracker(trackingId);
            }
        }

        private void RemoveTracker(int trackingId)
        {
            this.trackedSkeletons[trackingId].Dispose();
            this.trackedSkeletons.Remove(trackingId);
        }

        private void ResetFaceTracking()
        {
            foreach (int trackingId in new List<int>(this.trackedSkeletons.Keys))
            {
                this.RemoveTracker(trackingId);
            }
        }

        private class SkeletonFaceTracker : IDisposable
        {
            private static FaceTriangle[] faceTriangles;
            //
            private Rect faceRect;

            private EnumIndexableCollection<FeaturePoint, PointF> facePoints;

            private EnumIndexableCollection<FeaturePoint, Vector3DF> face3DPoints;
            //
            private FaceTracker faceTracker;

            private bool lastFaceTrackSucceeded;

            private SkeletonTrackingState skeletonTrackingState;

            public int LastTrackedFrame { get; set; }
            //
            public bool LastFaceTrackSucceeded
            {
                get { return this.lastFaceTrackSucceeded; }
            }

            public Rect FaceRect
            {
                get { return this.faceRect; }
            }

            public EnumIndexableCollection<FeaturePoint, PointF> FacePoints
            {
                get { return this.facePoints; }
            }

            public EnumIndexableCollection<FeaturePoint, Vector3DF> Face3DPoints
            {
                get { return this.face3DPoints; }
            }
            //
            public void Dispose()
            {
                if (this.faceTracker != null)
                {
                    this.faceTracker.Dispose();
                    this.faceTracker = null;
                }
            }

            public void DrawFaceModel(DrawingContext drawingContext)
            {
                if (!this.lastFaceTrackSucceeded || this.skeletonTrackingState != SkeletonTrackingState.Tracked)
                {
                    return;
                }

                var faceModelPts = new List<Point>();
                var faceModel = new List<FaceModelTriangle>();

                for (int i = 0; i < this.facePoints.Count; i++)
                {
                    faceModelPts.Add(new Point(this.facePoints[i].X + 0.5f, this.facePoints[i].Y + 0.5f));
                }

                foreach (var t in faceTriangles)
                {
                    var triangle = new FaceModelTriangle();
                    triangle.P1 = faceModelPts[t.First];
                    triangle.P2 = faceModelPts[t.Second];
                    triangle.P3 = faceModelPts[t.Third];
                    faceModel.Add(triangle);
                }

                var faceModelGroup = new GeometryGroup();
                for (int i = 0; i < faceModel.Count; i++)
                {
                    var faceTriangle = new GeometryGroup();
                    faceTriangle.Children.Add(new LineGeometry(faceModel[i].P1, faceModel[i].P2));
                    faceTriangle.Children.Add(new LineGeometry(faceModel[i].P2, faceModel[i].P3));
                    faceTriangle.Children.Add(new LineGeometry(faceModel[i].P3, faceModel[i].P1));
                    faceModelGroup.Children.Add(faceTriangle);
                }

                drawingContext.DrawGeometry(Brushes.LightYellow, new Pen(Brushes.LightYellow, 1.0), faceModelGroup);
            }

            public void DrawLandmark(DrawingContext drawingContext)
            {
                if (!this.lastFaceTrackSucceeded || this.skeletonTrackingState != SkeletonTrackingState.Tracked)
                {
                    return;
                }

                drawingContext.DrawEllipse(null, new Pen(Brushes.Red, 1.5), new Point(this.facePoints[21].X, this.facePoints[21].Y), 2, 2);
                drawingContext.DrawEllipse(null, new Pen(Brushes.Blue, 1.5), new Point(this.facePoints[54].X, this.facePoints[54].Y), 2, 2);
                drawingContext.DrawEllipse(null, new Pen(Brushes.Yellow, 1.5), new Point(this.facePoints[5].X, this.facePoints[5].Y), 2, 2);
                drawingContext.DrawEllipse(null, new Pen(Brushes.Green, 1.5), new Point(this.facePoints[88].X, this.facePoints[88].Y), 2, 2);
                drawingContext.DrawEllipse(null, new Pen(Brushes.OrangeRed, 1.5), new Point(this.facePoints[89].X, this.facePoints[89].Y), 2, 2);
                drawingContext.DrawEllipse(null, new Pen(Brushes.White, 1.5), new Point(this.facePoints[10].X, this.facePoints[10].Y), 2, 2);
            }

            /// <summary>
            /// Updates the face tracking information for this skeleton
            /// </summary>
            internal void OnFrameReady(KinectSensor kinectSensor, ColorImageFormat colorImageFormat, byte[] colorImage, DepthImageFormat depthImageFormat, short[] depthImage, Skeleton skeletonOfInterest)
            {
                this.skeletonTrackingState = skeletonOfInterest.TrackingState;

                if (this.skeletonTrackingState != SkeletonTrackingState.Tracked)
                {
                    // nothing to do with an untracked skeleton.
                    return;
                }

                if (this.faceTracker == null)
                {
                    try
                    {
                        this.faceTracker = new FaceTracker(kinectSensor);
                    }
                    catch (InvalidOperationException)
                    {
                        // During some shutdown scenarios the FaceTracker
                        // is unable to be instantiated.  Catch that exception
                        // and don't track a face.
                        Debug.WriteLine("AllFramesReady - creating a new FaceTracker threw an InvalidOperationException");
                        this.faceTracker = null;
                    }
                }

                if (this.faceTracker != null)
                {
                    FaceTrackFrame frame = this.faceTracker.Track(
                        colorImageFormat, colorImage, depthImageFormat, depthImage, skeletonOfInterest);

                    this.lastFaceTrackSucceeded = frame.TrackSuccessful;
                    if (this.lastFaceTrackSucceeded)
                    {
                        if (faceTriangles == null)
                        {
                            // only need to get this once.  It doesn't change.
                            faceTriangles = frame.GetTriangles();
                        }

                        this.faceRect = frame.FaceRect;
                        this.facePoints = frame.GetProjected3DShape();
                        this.face3DPoints = frame.Get3DShape();
                    }
                }
            }

            private struct FaceModelTriangle
            {
                public Point P1;
                public Point P2;
                public Point P3;
            }
        }
    }
}