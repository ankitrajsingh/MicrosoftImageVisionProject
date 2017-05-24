using Microsoft.ProjectOxford.Emotion;
using Microsoft.ProjectOxford.Face;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace UWPImageProject
{
    
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
            this.Loaded += OnLoaded;
        }

        void OnLoaded(object sender, RoutedEventArgs e)
        {
            this.cameraDisplay.SetFrameProcessor(async (bitmap) => await Task.Delay(16));
            this.cameraDisplay.ShowCamera(true);
        }
        async void OnTake(object sender, RoutedEventArgs e)
        {
            if(this.cameraDisplay.CapturedPhotoStream != null)
            {
                this.cameraDisplay.ResetVisuals();
            }
            else
            {
                await this.cameraDisplay.TakePhotoAsync();
            }
        }

        async void OnEmotion(object sender, RoutedEventArgs e)
        {
            if (this.cameraDisplay.CapturedPhotoStream != null)
            {
                var emotionClient = new EmotionServiceClient(Keys.EmotionKey);
                var results = await emotionClient.RecognizeAsync(this.cameraDisplay.CapturedPhotoStream.AsStreamForRead());
                var legend = new StringBuilder();
                foreach (var person in results)
                {
                    var emotionScores = person.Scores.AsDictionary();
                    var labelledScores =
                        emotionScores.OrderByDescending
                        (entry => entry.Value).
                        Select(
                            entry => new KeyValuePair<string, string>(
                                entry.Key,
                            LabelFromConfidenceValue(entry.Key, entry.Value)));
                    var listOfScores = string.Join(
                    ", ", labelledScores.Select(entry => entry.Value));
                    legend.AppendLine(listOfScores);
                }
                this.cameraDisplay.ShowLegend(legend.ToString());
            }

        }

        async void OnFace(object sender, RoutedEventArgs e)
        {
            if (this.cameraDisplay.CapturedPhotoStream != null)
            {
                
                FaceServiceClient client = new FaceServiceClient(Keys.FaceKey);
                var results = await client.DetectAsync(
                    this.cameraDisplay.CapturedPhotoStream.AsStreamForRead(),
                    true, true, new FaceAttributeType[]
                    {
                        FaceAttributeType.Age,
                        FaceAttributeType.FacialHair,
                        FaceAttributeType.Gender,
                        FaceAttributeType.HeadPose,
                        FaceAttributeType.Smile
                    });
                if (results != null)
                {
                    foreach (var face in results)
                    {
                        
                        this.cameraDisplay.DrawBox(face.FaceRectangle);
                       
                        foreach (var landmark in face.FaceLandmarks.AsDictionary())
                        {
                            var name = landmark.Key;
                            var coordinate = landmark.Value;
                            this.cameraDisplay.DrawLandmark(name, coordinate);
                        }
                        
                        var attributes = face.FaceAttributes;
                        var beard = LabelFromConfidenceValue(
                            "beard", attributes.FacialHair.Beard);
                        var moustache = LabelFromConfidenceValue(
                            "moustache", attributes.FacialHair.Moustache);
                        var sideburns = LabelFromConfidenceValue("sideburns", attributes.FacialHair.Sideburns);
                        var smile = LabelFromConfidenceValue(
                            "smile", attributes.Smile);
                        string legend = $"{attributes.Age} year old " +
                            $"{attributes.Gender} with " +
                            $"{smile},{beard},{moustache},{sideburns}";
                        this.cameraDisplay.ShowLegend(legend);
                    }
                }

            }
        }
       static string LabelFromConfidenceValue(string label, double confidence)
        {
            var returnLabel = label;
            if(confidence < 0.5)
            {
                returnLabel = $"no {label}";
            }
            return (returnLabel);
        }
    }
}
