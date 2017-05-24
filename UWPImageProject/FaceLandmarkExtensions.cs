using Microsoft.ProjectOxford.Emotion.Contract;
using Microsoft.ProjectOxford.Face.Contract;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace UWPImageProject
{
    static class EmotionExtensions
    {
        public static Dictionary<string, float> AsDictionary(
            this Scores scores)
        {
            Dictionary<string, float> returnScores = new Dictionary<string, float>();
            TypeInfo typeInfo = scores.GetType().GetTypeInfo();
            foreach (var property in typeInfo.DeclaredProperties)
            {
                var value = (float)property.GetValue(scores);
                returnScores.Add(property.Name, value);
            }
            return (returnScores);
        }
    }
    static class FaceLandmarkExtensions
    {
        public static Dictionary<string, FeatureCoordinate> AsDictionary(
            this FaceLandmarks landmarks)
        {
            Dictionary<string, FeatureCoordinate> coords = new Dictionary<string, FeatureCoordinate>() ;
            TypeInfo typeInfo = landmarks.GetType().GetTypeInfo();
            foreach(var property in typeInfo.DeclaredProperties)
            {
                var value = property.GetValue(landmarks) as FeatureCoordinate;
                coords.Add(property.Name, value);
            }
            return (coords);
        }
    }
}
