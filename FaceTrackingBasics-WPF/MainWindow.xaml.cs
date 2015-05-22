// -----------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace FaceTrackingBasics
{
    using System;
    using System.IO;
    using System.Windows;
    using System.Windows.Data;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using Microsoft.Kinect;
    using Microsoft.Kinect.Toolkit;
    using System.Runtime.InteropServices;
    using System.Text; 

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        //引入注册人脸模块的Registration的DLL
        [DllImport("D:\\BS\\DLL\\Registration_dll\\Debug\\Registration_dll.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void perform_shu();

        //引入识别人脸模块Identify的DLL
        [DllImport("D:\\BS\\DLL\\Identify_dll\\Debug\\Identify_dll.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void identify_shu();

        private static readonly int Bgr32BytesPerPixel = (PixelFormats.Bgr32.BitsPerPixel + 7) / 8;
        private readonly KinectSensorChooser sensorChooser = new KinectSensorChooser();
        private WriteableBitmap colorImageWritableBitmap;
        private byte[] colorImageData;
        private ColorImageFormat currentColorImageFormat = ColorImageFormat.Undefined;
        private string faceInfoSaveRoot = "D:\\BS\\shu_face";
        private string probe_file;
        public MainWindow()
        {
            InitializeComponent();
            var faceTrackingViewerBinding = new Binding("Kinect") { Source = sensorChooser };
            faceTrackingViewer.SetBinding(FaceTrackingViewer.KinectProperty, faceTrackingViewerBinding);

            sensorChooser.KinectChanged += SensorChooserOnKinectChanged;

            sensorChooser.Start();
        }

        private void SensorChooserOnKinectChanged(object sender, KinectChangedEventArgs kinectChangedEventArgs)
        {
            KinectSensor oldSensor = kinectChangedEventArgs.OldSensor;
            KinectSensor newSensor = kinectChangedEventArgs.NewSensor;

            if (oldSensor != null)
            {
                oldSensor.AllFramesReady -= KinectSensorOnAllFramesReady;
                oldSensor.ColorStream.Disable();
                oldSensor.DepthStream.Disable();
                oldSensor.DepthStream.Range = DepthRange.Default;
                oldSensor.SkeletonStream.Disable();
                oldSensor.SkeletonStream.EnableTrackingInNearRange = false;
                oldSensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Default;
            }

            if (newSensor != null)
            {
                try
                {
                    newSensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);
                    newSensor.DepthStream.Enable(DepthImageFormat.Resolution320x240Fps30);
                    try
                    {
                        // This will throw on non Kinect For Windows devices.
                        newSensor.DepthStream.Range = DepthRange.Near;
                        newSensor.SkeletonStream.EnableTrackingInNearRange = true;
                    }
                    catch (InvalidOperationException)
                    {
                        newSensor.DepthStream.Range = DepthRange.Default;
                        newSensor.SkeletonStream.EnableTrackingInNearRange = false;
                    }

                    newSensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Seated;
                    newSensor.SkeletonStream.Enable();
                    newSensor.AllFramesReady += KinectSensorOnAllFramesReady;
                }
                catch (InvalidOperationException)
                {
                    // This exception can be thrown when we are trying to
                    // enable streams on a device that has gone away.  This
                    // can occur, say, in app shutdown scenarios when the sensor
                    // goes away between the time it changed status and the
                    // time we get the sensor changed notification.
                    //
                    // Behavior here is to just eat the exception and assume
                    // another notification will come along if a sensor
                    // comes back.
                }
            }
        }

        private void WindowClosed(object sender, EventArgs e)
        {
            sensorChooser.Stop();
            faceTrackingViewer.Dispose();
        }

        private void KinectSensorOnAllFramesReady(object sender, AllFramesReadyEventArgs allFramesReadyEventArgs)
        {
            using (var colorImageFrame = allFramesReadyEventArgs.OpenColorImageFrame())
            {
                if (colorImageFrame == null)
                {
                    return;
                }

                // Make a copy of the color frame for displaying.
                var haveNewFormat = this.currentColorImageFormat != colorImageFrame.Format;
                if (haveNewFormat)
                {
                    this.currentColorImageFormat = colorImageFrame.Format;
                    this.colorImageData = new byte[colorImageFrame.PixelDataLength];
                    this.colorImageWritableBitmap = new WriteableBitmap(
                        colorImageFrame.Width, colorImageFrame.Height, 96, 96, PixelFormats.Bgr32, null);
                    colorImage.Source = this.colorImageWritableBitmap;
                }

                colorImageFrame.CopyPixelDataTo(this.colorImageData);
                this.colorImageWritableBitmap.WritePixels(
                    new Int32Rect(0, 0, colorImageFrame.Width, colorImageFrame.Height),
                    this.colorImageData,
                    colorImageFrame.Width * Bgr32BytesPerPixel,
                    0);
            }
        }


        private void Register_Button(object sender, RoutedEventArgs e)//采集人脸、进行注册
        {
            //identify_shu();
            if(this.nameInputBox.Text == "")
            {
                this.statusLabel.Content = "未输入信息";
                return;
            }

            string probe_name = this.nameInputBox.Text.Trim();
            string userFaceInfoRoot = this.faceInfoSaveRoot + "\\probe";
            try
            {
                if(!Directory.Exists(userFaceInfoRoot))
                {
                    Directory.CreateDirectory(userFaceInfoRoot);
                }
            }
            catch(Exception excep)
            {
                Console.WriteLine("存储人脸过程失败: {0}", excep.ToString());
            }

            int faceCount = faceTrackingViewer.WriteFaceInfo(userFaceInfoRoot, probe_name);
            probe_file = userFaceInfoRoot + "\\" + probe_name + ".txt";
            if(faceCount == 0)
            {
                //未检测到人脸
                this.statusLabel.Content = "未检测到人脸";
            }
            else
            {
                //采集成功
                this.statusLabel.Content = "人脸采集成功";
            }

            //加入人脸注册模块
            perform_shu();
        }

        private void Identify_Button(object sender, RoutedEventArgs e)//计算人脸距离，显示识别结果
        {
            identify_shu();
            BitmapImage imagetemp = new BitmapImage(new Uri("D:\\BS\\shu_face\\result\\rgb_result.bmp", UriKind.Absolute));
            this.result_image.Source = imagetemp;
            StreamReader sr = new StreamReader("D:\\BS\\shu_face\\result\\info_result.txt", Encoding.UTF8);
            string line = sr.ReadLine();
            this.ResultBox.Text = line;
            this.statusLabel.Content = "识别结束";
        }
    }
}
