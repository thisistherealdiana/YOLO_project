using Microsoft.ML;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using static Microsoft.ML.Transforms.Image.ImageResizingEstimator;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Globalization;
using System.Collections.Concurrent;

namespace MyLibrary
{
    public class ImageAnalizer
    {
        const string modelPath = @"C:\Users\archi\OneDrive\Рабочий стол\yolov4.onnx";

        static readonly string[] classesNames = new string[] { "person", "bicycle", "car", "motorbike", "aeroplane", "bus", "train", "truck", "boat", "traffic light", "fire hydrant", "stop sign", "parking meter", "bench", "bird", "cat", "dog", "horse", "sheep", "cow", "elephant", "bear", "zebra", "giraffe", "backpack", "umbrella", "handbag", "tie", "suitcase", "frisbee", "skis", "snowboard", "sports ball", "kite", "baseball bat", "baseball glove", "skateboard", "surfboard", "tennis racket", "bottle", "wine glass", "cup", "fork", "knife", "spoon", "bowl", "banana", "apple", "sandwich", "orange", "broccoli", "carrot", "hot dog", "pizza", "donut", "cake", "chair", "sofa", "pottedplant", "bed", "diningtable", "toilet", "tvmonitor", "laptop", "mouse", "remote", "keyboard", "cell phone", "microwave", "oven", "toaster", "sink", "refrigerator", "book", "clock", "vase", "scissors", "teddy bear", "hair drier", "toothbrush" };

        public static async Task imagesAnalizer(string imageFolder, CancellationToken token, ConcurrentQueue<IReadOnlyList<YoloV4Result>> queue)
        {
            MLContext mlContext = new MLContext();
            // Define scoring pipeline
            var pipeline = mlContext.Transforms.ResizeImages(inputColumnName: "bitmap", outputColumnName: "input_1:0", imageWidth: 416, imageHeight: 416, resizing: ResizingKind.IsoPad)
                .Append(mlContext.Transforms.ExtractPixels(outputColumnName: "input_1:0", scaleImage: 1f / 255f, interleavePixelColors: true))
                .Append(mlContext.Transforms.ApplyOnnxModel(
                    shapeDictionary: new Dictionary<string, int[]>()
                    {
                        { "input_1:0", new[] { 1, 416, 416, 3 } },
                        { "Identity:0", new[] { 1, 52, 52, 3, 85 } },
                        { "Identity_1:0", new[] { 1, 26, 26, 3, 85 } },
                        { "Identity_2:0", new[] { 1, 13, 13, 3, 85 } },
                    },
                    inputColumnNames: new[]
                    {
                        "input_1:0"
                    },
                    outputColumnNames: new[]
                    {
                        "Identity:0",
                        "Identity_1:0",
                        "Identity_2:0"
                    },
                    modelFile: modelPath, recursionLimit: 100));
            
            // Fit on empty list to obtain input data schema
            var model = pipeline.Fit(mlContext.Data.LoadFromEnumerable(new List<YoloV4BitmapData>()));

            // Create prediction engine
            var predictionEngine = mlContext.Model.CreatePredictionEngine<YoloV4BitmapData, YoloV4Prediction>(model);


            //getting results
            string[] fileNames = Directory.GetFiles(imageFolder);

            
            object locker = new object();

            var createBitmaps = new TransformBlock<string, Bitmap>(name =>
            {
                var bitmap = new Bitmap(Image.FromFile(name));
                return bitmap;
                                
            },
            new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount,
                CancellationToken = token
            });


            var predictObjects = new TransformBlock<Bitmap, YoloV4Prediction>(bitmap =>
            {
                YoloV4Prediction predict = predictionEngine.Predict(new YoloV4BitmapData() { Image = bitmap });
                return predict;
            },
            new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = 1,
                CancellationToken = token
            });

            var gettingResults = new ActionBlock<YoloV4Prediction>(predict =>
            {
                if (!token.IsCancellationRequested)
                {
                    var results = predict.GetResults(classesNames, 0.3f, 0.7f);
                    queue.Enqueue(results);
                }
            },
            new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount,
                CancellationToken = token
            });

            var linkOptions = new DataflowLinkOptions { PropagateCompletion = true };

            createBitmaps.LinkTo(predictObjects, linkOptions);
            predictObjects.LinkTo(gettingResults,linkOptions);

            Parallel.For(0, fileNames.Length, i => createBitmaps.Post(fileNames[i]));
            createBitmaps.Complete();
            await gettingResults.Completion;
        }
    }
}
