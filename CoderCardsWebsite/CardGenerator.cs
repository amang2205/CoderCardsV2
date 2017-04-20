using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using System;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using static CodermonCards.ImageHelpers;

namespace CodermonCards
{
    public class CodermonCardsGenerator
    {
        public static async Task Run(byte[] image, string filename, Stream outputBlob, TraceWriter log)
        {
            string result = await CallEmotionAPI(image);
            log.Info(result);

            if (String.IsNullOrEmpty(result))
            {
                log.Error("No result from Emotion API");
                return;
            }

            var imageData = JsonConvert.DeserializeObject<Face[]>(result);

            if (imageData.Length == 0)
            {
                log.Error("No face detected in image");
                return;
            }

            double score = 0;
            var faceData = imageData[0]; // assume exactly one face
            var card = GetCardImageAndScores(faceData.Scores, out score);

            var personInfo = GetNameAndTitle(filename); // extract name and title from filename
            MergeCardImage(card, image, personInfo, score);

            SaveAsJpeg(card, outputBlob);
        }

        public static Tuple<string, string> GetNameAndTitle(string filename)
        {
            string[] words = filename.Split('-');

            return words.Length > 1 ? Tuple.Create(words[0], words[1]) : Tuple.Create("", "");
        }

        static Image GetCardImageAndScores(Scores scores, out double score)
        {
            NormalizeScores(scores);

            var cardBack = "neutral.png";
            score = scores.Neutral;
            const int angerBoost = 2, happyBoost = 4;

            if (scores.Surprise > 10)
            {
                cardBack = "surprised.png";
                score = scores.Surprise;
            }
            else if (scores.Anger > 10)
            {
                cardBack = "angry.png";
                score = scores.Anger * angerBoost;
            }
            else if (scores.Happiness > 50)
            {
                cardBack = "happy.png";
                score = scores.Happiness * happyBoost;
            }

            var thisExe = System.Reflection.Assembly.GetExecutingAssembly();
            var file = thisExe.GetManifestResourceStream($"CodermonCards.CardGenerator.{cardBack}");

            return Image.FromStream(file);
        }

        static async Task<string> CallEmotionAPI(byte[] image)
        {
            var client = new HttpClient();
            var content = new StreamContent(new MemoryStream(image));
            var key = Environment.GetEnvironmentVariable("EMOTION_API_KEY_NAME");

            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", key);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            var httpResponse = await client.PostAsync(Environment.GetEnvironmentVariable("EMOTION_API_URI"), content);

            if (httpResponse.StatusCode == HttpStatusCode.OK)
            {
                return await httpResponse.Content.ReadAsStringAsync();
            }

            return null;
        }
    }
}