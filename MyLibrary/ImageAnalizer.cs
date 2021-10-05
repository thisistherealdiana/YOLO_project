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

namespace MyLibrary
{
    public class ImageAnalizer
    {
        const string modelPath = @"C:\Users\archi\OneDrive\Рабочий стол\yolov4.onnx";

        static readonly string[] classesNames = new string[] { "person", "bicycle", "car", "motorbike", "aeroplane", "bus", "train", "truck", "boat", "traffic light", "fire hydrant", "stop sign", "parking meter", "bench", "bird", "cat", "dog", "horse", "sheep", "cow", "elephant", "bear", "zebra", "giraffe", "backpack", "umbrella", "handbag", "tie", "suitcase", "frisbee", "skis", "snowboard", "sports ball", "kite", "baseball bat", "baseball glove", "skateboard", "surfboard", "tennis racket", "bottle", "wine glass", "cup", "fork", "knife", "spoon", "bowl", "banana", "apple", "sandwich", "orange", "broccoli", "carrot", "hot dog", "pizza", "donut", "cake", "chair", "sofa", "pottedplant", "bed", "diningtable", "toilet", "tvmonitor", "laptop", "mouse", "remote", "keyboard", "cell phone", "microwave", "oven", "toaster", "sink", "refrigerator", "book", "clock", "vase", "scissors", "teddy bear", "hair drier", "toothbrush" };

        public static void printToConsole(YoloV4Result res)
        {
            var x1 = res.BBox[0];
            var y1 = res.BBox[1];
            var x2 = res.BBox[2];
            var y2 = res.BBox[3];
            Console.WriteLine($"In a rectangle [left,top,right,bottom]:[{x1.ToString("F1", CultureInfo.InvariantCulture)}; " +
                $"{y1.ToString("F1", CultureInfo.InvariantCulture)}; {x2.ToString("F1", CultureInfo.InvariantCulture)}; " +
                $"{y2.ToString("F1", CultureInfo.InvariantCulture)}] was/were found (a) {res.Label}.");
        }
        public static async Task imagesAnalizer(string imageFolder)
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

            var sw = new Stopwatch();
            sw.Start();

            //defining cancellation token
            CancellationTokenSource source = new CancellationTokenSource();
            //CancellationToken token = source.Token;

            //getting results
            string[] fileNames = Directory.GetFiles(imageFolder);

            
            object locker = new object();
            /*
            var ab = new ActionBlock<string>(async name =>
            {                
                YoloV4Prediction predict;
                lock (locker)
                {
                    var bitmap = new Bitmap(Image.FromFile(name));
                    predict = predictionEngine.Predict(new YoloV4BitmapData() { Image = bitmap });  
                }
                var results = predict.GetResults(classesNames, 0.3f, 0.7f);

                foreach (var res in results)
                {                  
                    printToConsole(res);
                }     
            },
            new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = 4,
                CancellationToken=source.Token
            });

            var buf = new BufferBlock<string>();
            buf.LinkTo(ab);

            Parallel.For(0, fileNames.Length, i => buf.Post(fileNames[i]));
            ab.Complete();

            await ab.Completion;
            */
            //var batch = new BatchBlock<string>(fileNames.Length);
            var createBitmaps = new TransformBlock<string, Bitmap>(name =>
            {
                var bitmap = new Bitmap(Image.FromFile(name));
                return bitmap;   
            },
            new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = 4,
                CancellationToken = source.Token
            });


            var predictObjects = new TransformBlock<Bitmap, YoloV4Prediction>(bitmap =>
            {
                lock (locker)
                {
                    YoloV4Prediction predict = predictionEngine.Predict(new YoloV4BitmapData() { Image = bitmap });
                    return predict;
                }
            },
            new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = 4,
                CancellationToken = source.Token
            });

            var gettingResults = new TransformBlock<YoloV4Prediction, IReadOnlyList<YoloV4Result>>(predict =>
            {
                var results = predict.GetResults(classesNames, 0.3f, 0.7f);
                return results;
            },
            new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = 4,
                CancellationToken = source.Token
            });

            var printResults = new ActionBlock<IReadOnlyList<YoloV4Result>>(res=>
            {
                for (int i = 0; i < res.Count; i++)
                    printToConsole(res[i]);
            },
            new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = 4,
                CancellationToken = source.Token
            });

            var linkOptions = new DataflowLinkOptions { PropagateCompletion = true };

            //batch.LinkTo(createBitmaps, linkOptions);
            createBitmaps.LinkTo(predictObjects, linkOptions);
            predictObjects.LinkTo(gettingResults,linkOptions);
            gettingResults.LinkTo(printResults, linkOptions);

            Parallel.For(0, fileNames.Length, i => createBitmaps.Post(fileNames[i]));
            createBitmaps.Complete();
            await printResults.Completion;

            sw.Stop();
            Console.WriteLine($"Done in {sw.ElapsedMilliseconds}ms.");


            //EXAMPLE 
            /*
            var ab = new ActionBlock<int>(async i =>
            {
                var r = new Random();
                Console.Write("{");
                await Task.Delay(r.Next(1000));
                Console.WriteLine("}");
            },
            new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = 4
            });
            Parallel.For(0, 100, i => ab.Post(i));
            ab.Complete();

            await ab.Completion;
            */
        }
    }
}
