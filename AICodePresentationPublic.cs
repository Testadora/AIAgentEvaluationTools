using NUnit.Framework;
using Microsoft.Playwright;
using System.Text.Json; 

namespace BRG.QA.Tests.Presentations.AIAgentTests
{    
    public class AzureVisionClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _endpoint;
        private readonly string _apiKey;

        public AzureVisionClient(string endpoint, string apiKey)
        {
            _endpoint = endpoint.TrimEnd('/');
            _apiKey = apiKey;
            _httpClient = new HttpClient();
        }
        public async Task<HttpResponseMessage> VectorizeImageFromFileAsync(string imageFilePath)
        {
            string url = $"{_endpoint}/{ComputerVisionConfig.computerVisionModel}";

            // Read the image file into a byte array
            byte[] imageData = await File.ReadAllBytesAsync(imageFilePath);
            var content = new ByteArrayContent(imageData);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

            // Set headers
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("Ocp-Apim-Subscription-Key", _apiKey);
            request.Content = content;

            // Send request
            var response = await _httpClient.SendAsync(request);

            return response;
        }
 
    }
    public static class ComputerVisionConfig
    {
        public static readonly string computerVisionModel = @"computervision/retrieval:vectorizeImage?model-version=2023-04-15&api-version=2024-02-01"; // most recent known model Azure Computer Vision as of 2/1/26
        public static readonly string computerVisionResourceEndpoint = "https://<yourCustomResourceName>.cognitiveservices.azure.com/"; // endpoint URL for the Computer Vision resource, defined in Azure subscription/resource group
        public static readonly string computerVisionResourceKey = "<yourCustomKeyString>";
    }
    
    public class AIComputerVisionIntegration
    {
        public static async Task<(string, float[])> GetModelAndVectorFromVectorizationResponseAsync(HttpResponseMessage actualResponse)
        {
            var jsonResponse = await ConvertHttpResponseToJsonElements(actualResponse);
            // Extract vector
            float[] vector = jsonResponse.TryGetValue("vector", out var vectorElement) &&
                                vectorElement.ValueKind == JsonValueKind.Array
                                ? vectorElement.EnumerateArray().Select(v => v.GetSingle()).ToArray()
                                : Array.Empty<float>();
            // Extract modelVersion
            string? modelVersion = jsonResponse.TryGetValue("modelVersion", out var modelVersionElement) &&
                                    modelVersionElement.ValueKind == JsonValueKind.String
                                    ? modelVersionElement.GetString()
                                    : string.Empty;
            return (modelVersion, vector);
        }
        public static float ComputeCosineSimilarity(float[] vectorA, float[] vectorB)
        {
            if (vectorA.Length != vectorB.Length || vectorA.Length == 0)
            {
                return 0f;
            }

            float dotProduct = 0f;
            float magnitudeA = 0f;
            float magnitudeB = 0f;

            for (int i = 0; i < vectorA.Length; i++)
            {
                dotProduct += vectorA[i] * vectorB[i];
                magnitudeA += vectorA[i] * vectorA[i];
                magnitudeB += vectorB[i] * vectorB[i];
            }

            float denominator = (float)(Math.Sqrt(magnitudeA) * Math.Sqrt(magnitudeB));
            return denominator > 0 ? dotProduct / denominator : 0f;
        }
        public static async Task<Dictionary<string, JsonElement>> ConvertHttpResponseToJsonElements(HttpResponseMessage httpResponse)
        {
            string response = await httpResponse.Content.ReadAsStringAsync();
            var jsonResponse = await ConvertHttpResponseToJsonDocument(httpResponse);
            var extractedNodes = new Dictionary<string, JsonElement>();
            JsonElement modelVersion = jsonResponse.RootElement.GetProperty("modelVersion");
            JsonElement vector = jsonResponse.RootElement.GetProperty("vector");
            extractedNodes.Add("modelVersion", modelVersion);
            extractedNodes.Add("vector", vector);

            return extractedNodes;
        }
        public static async Task<JsonDocument> ConvertHttpResponseToJsonDocument(HttpResponseMessage httpResponse)
        {
            string response = await httpResponse.Content.ReadAsStringAsync();
            JsonDocument jsonResponse = JsonDocument.Parse(response);
            return jsonResponse;
        }
    }


    [TestFixture]
    public class AIEvaluationTests
    {
        private const string BaseUrl = "https://blueridgegateways.com";
        private const int ViewportWidth = 1280;
        private const int ViewportHeight = 720;
        private const float Tolerance = 0.98f; // Configurable tolerance [0.0 - 1.0]
        private static readonly string BaselineDir = @"C:\QAAutomation\TestInput\BaselineImages\ImgCompareMethodTests";
        private static readonly string SnapshotDir = @"C:\QAAutomation\TestOutput\Snapshots\ImgCompareMethodTests";
        private static readonly string ArchiveDir = @"C:\QAAutomation\TestOutput\Snapshots\ImgCompareMethodTests\VisionEvalTestSnapshots";
        private static readonly string BaselineImageName = "baselineImage.png";
        private static readonly string ActualImageName = "blueridgegateways_homepage.png";
        private AzureVisionClient _azureVisionClient;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // initialize Azure Vision Client to be used for all tests in this class
            _azureVisionClient = new AzureVisionClient(
                ComputerVisionConfig.computerVisionResourceEndpoint,
                ComputerVisionConfig.computerVisionResourceKey
            );
        }

        [Test]
        [Category("UI")]
        [Category("AI")]
        [Category("ComputerVision")]
        [Category("EvaluateComputerVision")]
        public async Task HomepageSnapshot_ShouldMeetCosineSimilarityTolerance()
        {
            var pngPath = Path.Combine(SnapshotDir, "blueridgegateways_homepage.png");
            var jpgPath = Path.Combine(SnapshotDir, "blueridgegateways_homepage.jpg");
            Directory.CreateDirectory(SnapshotDir);

            #region test setup, ensure clean entry into test, clean up files from prior runs
            // assure clean entry into test by deleting any existing snapshot files in working directory
            if (File.Exists(pngPath))
            {
                File.Delete(pngPath);
            }

            if (File.Exists(jpgPath))
            {
                File.Delete(jpgPath);
            }
            #endregion test setup, ensure clean entry into test, clean up files from prior runs and prepare directory

            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true
                //Headless = false
            });
            var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                ViewportSize = new ViewportSize
                {
                    Width = ViewportWidth,
                    Height = ViewportHeight
                },
                DeviceScaleFactor = 1.0f,
                IsMobile = false,
                HasTouch = false
            });
            var page = await context.NewPageAsync();
            // Normalize window size (useful for some environments)
            await page.SetViewportSizeAsync(ViewportWidth, ViewportHeight);
            await page.GotoAsync(BaseUrl);
            await page.WaitForSelectorAsync("body", new PageWaitForSelectorOptions
            {
                State = WaitForSelectorState.Visible
            });
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = pngPath,
                FullPage = true,
                Type = ScreenshotType.Png
            });
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = jpgPath,
                FullPage = true,
                Type = ScreenshotType.Jpeg,
                Quality = 90
            });

            var baselineImage = Path.Combine(BaselineDir, BaselineImageName);
            var compareImage = Path.Combine(SnapshotDir, ActualImageName);
            var baselineResponse = await _azureVisionClient.VectorizeImageFromFileAsync(baselineImage);
            var baselineModelAndVector = await AIComputerVisionIntegration.GetModelAndVectorFromVectorizationResponseAsync(baselineResponse);
            string baselineModelVersion = baselineModelAndVector.Item1;
            float[] baselineVector = baselineModelAndVector.Item2;
            var actualResponse = await _azureVisionClient.VectorizeImageFromFileAsync(compareImage);
            var actualModelAndVector = await AIComputerVisionIntegration.GetModelAndVectorFromVectorizationResponseAsync(actualResponse);
            string actualModelVersion = actualModelAndVector.Item1;
            float[] actualVector = actualModelAndVector.Item2;

            float similarity = AIComputerVisionIntegration.ComputeCosineSimilarity(baselineVector, actualVector);

            Assert.That(similarity, Is.GreaterThanOrEqualTo(Tolerance),
                $"Cosine similarity {similarity} was below tolerance {Tolerance}.");
            Console.WriteLine($"Cosine similarity between baseline and actual image: {similarity}");
            Console.WriteLine($"Accepted Similarity is at a value greater than or equal to: {Tolerance}");

            #region archive results locally, clean up test
            // Define the archive folder name with timestamp
            string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            string archiveFolder = Path.Combine(ArchiveDir, $"{timestamp}.BRG.Vision.Test.Output");
            // Create the archive folder
            Directory.CreateDirectory(archiveFolder);
            // Define the paths of the snapshot images
            string pngPathArchive = Path.Combine(archiveFolder, "blueridgegateways_homepage.png");
            string jpgPathArchive = Path.Combine(archiveFolder, "blueridgegateways_homepage.jpg");
            // Copy the snapshot images to the archive folder
            if (File.Exists(pngPath))
            {
                File.Copy(pngPath, Path.Combine(archiveFolder, Path.GetFileName(pngPathArchive)));
                File.Delete(pngPath); // Delete the original file after copying
            }

            if (File.Exists(jpgPath))
            {
                File.Copy(jpgPath, Path.Combine(archiveFolder, Path.GetFileName(jpgPathArchive)));
                File.Delete(jpgPath); // Delete the original file after copying
            }
            #endregion archive results locally, clean up test
        }
    }
}

