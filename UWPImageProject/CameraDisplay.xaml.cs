using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Capture;
using Windows.ApplicationModel;
using Windows.System.Display;
using Windows.Graphics.Display;
using Windows.Graphics.Imaging;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Storage.Streams;
using Windows.Media;
using Microsoft.ProjectOxford.Face.Contract;
using Windows.UI.Xaml.Media.Imaging;
using Windows.Devices.Enumeration;
using Windows.Media.FaceAnalysis;
using Windows.Media.MediaProperties;


// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace UWPImageProject
{
    [ComImport]
    [Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    unsafe interface IMemoryBufferByteAccess
    {
        void GetBuffer(out byte* buffer, out uint capacity);
    }
    public sealed partial class CameraDisplay : UserControl
    {
        InMemoryRandomAccessStream currentPhotoStream;
        int currentPhotoWidth;
        int currentPhotoHeight;
        VideoFrame videoFrame;
        Func<SoftwareBitmap, Task> frameProcessor;
        MediaCapture mediaCapture;
        TaskCompletionSource<bool> capturingTask;
        Size previewVideoSize;
        volatile bool faceProcessingPaused;

        public CameraDisplay()
        {
            this.InitializeComponent();
            this.capturingTask = new TaskCompletionSource<bool>();
            this.Loaded += OnLoaded;
        }
        public void SetFrameProcessor(Func<SoftwareBitmap, Task> processor)
        {
           
            if(this.frameProcessor != null)
            {
                throw new InvalidOperationException("Alreasy set - sorry,can't change it");
            }
            this.frameProcessor = processor;
            Task.Run(
                async () =>
                {
                    await this.capturingTask.Task;
                    this.capturingTask = null;
                    while (true)
                    {
                        if (!this.faceProcessingPaused)
                        {
                            await this.mediaCapture.GetPreviewFrameAsync(this.videoFrame);
                            await this.Dispatcher.RunAsync(
                                Windows.UI.Core.CoreDispatcherPriority.Normal,
                                async () =>
                                {
                                    await this.frameProcessor(this.videoFrame.SoftwareBitmap);
                                }
                                );
                        }
                        else
                        {
                            await Task.Delay(1000);
                        }
                    }
                });
        }
        public void DrawLandmark(string name,FeatureCoordinate coordinate)
        {
            var landmark = new LandmarkControl(name);
            landmark.Loaded += (s, e) =>
            {
                Canvas.SetLeft(landmark,
                    this.ScaleXPhotoValueToCanvas((int)coordinate.X) - (landmark.ActualWidth / 2));
                Canvas.SetTop(landmark,
                    this.ScaleYPhotoValueToCanvas((int)coordinate.Y) - (landmark.ActualHeight / 2));
            };
            this.canvasLandmarks.Children.Add(landmark);
        }
        public void DrawBox(FaceRectangle faceBox)
        {
            if(this.canvasHighlight.Visibility != Visibility.Visible)
            {
                this.canvasHighlight.Visibility = Visibility.Visible;
            }
            
            int scaledX = this.ScaleXPhotoValueToCanvas((int)faceBox.Left);
            int scaledY = this.ScaleYPhotoValueToCanvas((int)faceBox.Top);
            int scaledWidth = this.ScaleXPhotoValueToCanvas((int)faceBox.Width);
            int scaledHeight = this.ScaleYPhotoValueToCanvas((int)faceBox.Height);

            Canvas.SetLeft(this.ellipseHighlight, scaledX);
            Canvas.SetTop(this.ellipseHighlight, scaledY);
            this.ellipseHighlight.Width = scaledWidth;
            this.ellipseHighlight.Height = scaledHeight;
        }
        public void ShowCamera(bool on=true)
        {
            if(on && (this.gridCover.Visibility != Visibility.Collapsed))
            {
                this.gridCover.Visibility = Visibility.Collapsed;
                this.ellipseHighlight.Visibility = Visibility.Visible;
            }
            else if(!on && (this.gridCover.Visibility != Visibility.Visible))
            {
                this.gridCover.Visibility = Visibility.Visible;
                this.ellipseHighlight.Visibility = Visibility.Collapsed;
            }
        }
        public void ShowLegend(string legend)
        {
            this.txtLegend.Text = legend;
            this.gridLegend.Visibility = Visibility.Visible;
        }
        public void ResetVisuals()
        {
            this.currentPhotoStream?.Dispose();
            this.currentPhotoStream = null;
            this.faceProcessingPaused = false;
            this.gridLegend.Visibility = Visibility.Collapsed;
            this.txtLegend.Text = string.Empty;
            this.canvasLandmarks.Children.Clear();
            this.canvasHighlight.Visibility = Visibility.Collapsed;
            this.imgSnap.Visibility = Visibility.Collapsed;
        }
        public async Task TakePhotoAsync()
        {
            this.faceProcessingPaused = true;
            
            SoftwareBitmapSource source = new SoftwareBitmapSource();
            SoftwareBitmap bgra8Bitmap = SoftwareBitmap.Convert(
                this.videoFrame.SoftwareBitmap, BitmapPixelFormat.Bgra8);
            await source.SetBitmapAsync(bgra8Bitmap);
            this.imgSnap.Source = source;
           
            this.currentPhotoStream?.Dispose();
            this.currentPhotoStream = new InMemoryRandomAccessStream();
            var encoder = await BitmapEncoder.CreateAsync(
                BitmapEncoder.JpegEncoderId, this.currentPhotoStream);
            encoder.SetSoftwareBitmap(bgra8Bitmap);
            await encoder.FlushAsync();
            this.currentPhotoWidth = bgra8Bitmap.PixelWidth;
            this.currentPhotoHeight = bgra8Bitmap.PixelHeight;

            this.canvasHighlight.Visibility = Visibility.Collapsed;
            this.imgSnap.Visibility = Visibility.Visible;
        }
        public InMemoryRandomAccessStream CapturedPhotoStream
        {
            get
            {
                return (this.currentPhotoStream);
            }
        }
        int ScaleXPhotoValueToCanvas(int x)
        {
            return (this.ScalePhotoValueToCanvas(x, this.currentPhotoWidth, this.canvasHighlight.ActualWidth));
        }
        int ScaleYPhotoValueToCanvas(int y)
        {
            return (this.ScalePhotoValueToCanvas(y, this.currentPhotoHeight, this.canvasHighlight.ActualHeight));
        }
        int ScalePhotoValueToCanvas(int value,double maxVideoValue, double maxCanvasValue)
        {
            double relativeValue = (value / maxVideoValue) * maxCanvasValue;
            return ((int)relativeValue);
        }
        async void OnLoaded(object sender,RoutedEventArgs e)
        {
            var cameras = await this.GetCameraDetailsAsync();
            var firstCamera = cameras.FirstOrDefault();
            if(firstCamera != null)
            {
                MediaCaptureInitializationSettings settings =
                    new MediaCaptureInitializationSettings();
                settings.VideoDeviceId = firstCamera.Id;
                this.mediaCapture = new MediaCapture();
                await this.mediaCapture.InitializeAsync(settings);
                this.InitialiseFlowDirectionFromCameraLocation(firstCamera);
                this.captureElement.Source = this.mediaCapture;
                await this.mediaCapture.StartPreviewAsync();
                this.InitialisePreviewSizesFromVideoDevice();
                this.InitialiseVideoFrameFromDetectorFormats();
                this.capturingTask.SetResult(true);

            }
        }
        void InitialiseFlowDirectionFromCameraLocation(DeviceInformation firstCamera)
        {
            var externalCamera =
                (firstCamera.EnclosureLocation?.Panel == Windows.Devices.Enumeration.Panel.Unknown);
            var frontCamera =
                (!externalCamera && firstCamera.EnclosureLocation?.Panel == Windows.Devices.Enumeration.Panel.Front);
            this.captureElement.FlowDirection =
                this.imgSnap.FlowDirection =
                this.canvasHighlight.FlowDirection =
                frontCamera ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        }
        void InitialisePreviewSizesFromVideoDevice()
        {
            var previewProperties =
        this.mediaCapture.VideoDeviceController.GetMediaStreamProperties(
        MediaStreamType.VideoPreview) as VideoEncodingProperties;

            this.previewVideoSize = new Size(
      previewProperties.Width, previewProperties.Height);
        }
        void InitialiseVideoFrameFromDetectorFormats()
        {
            var bitmapFormats = FaceDetector.GetSupportedBitmapPixelFormats();

            this.videoFrame = new VideoFrame(
              bitmapFormats.First(),
              (int)this.previewVideoSize.Width,
      (int)this.previewVideoSize.Height);
        }
        
        async Task<List<DeviceInformation>> GetCameraDetailsAsync()
        {
            var deviceInfo = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
            List<DeviceInformation> returnedList = new List<DeviceInformation>();
            if(deviceInfo != null)
            {
                returnedList = deviceInfo.OrderBy(
                    di =>
                    {
                        var sortOrder = 2;
                        if (di.EnclosureLocation != null)
                        {
                            sortOrder = di.EnclosureLocation.Panel
                            == Windows.Devices.Enumeration.Panel.Front ? 0 : 1;
                        }
                        return (sortOrder);
                    }).ToList();
            }
            return (returnedList);
        }
    }
}
