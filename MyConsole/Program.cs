using Microsoft.ML;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using static Microsoft.ML.Transforms.Image.ImageResizingEstimator;
using MyLibrary;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Threading;
using System.Collections.Concurrent;
using System.Globalization;

namespace MyConsole
{
    class Program
    {
        // model is available here:
        // https://github.com/onnx/models/tree/master/vision/object_detection_segmentation/yolov4

        const string modelPath = @"C:\Users\archi\OneDrive\Рабочий стол\yolov4.onnx";

        //full path to the input directory
        const string imageFolder = @"C:\Users\archi\OneDrive\Рабочий стол\lab\401_kereselidze\MyConsole\Assets\Images";
        //string imageFolder;

        //
        //const string imageOutputFolder = @"C:\Users\archi\OneDrive\Рабочий стол\lab\401_kereselidze\MyConsole\Assets\Output";
        static async Task Main()
        {
            Console.WriteLine("Press CTRL+C if you want the program terminated");
            //Console.WriteLine("Paste full path to the directory with images that you wish to have analized");
            //var imageFolder = Console.ReadLine(); 
            var sw = new Stopwatch();
            sw.Start();

            CancellationTokenSource source = new CancellationTokenSource();
            Console.CancelKeyPress += new ConsoleCancelEventHandler(myHandler);
            void myHandler(object sender, ConsoleCancelEventArgs args)
            {
                sw.Stop();
                Console.WriteLine($"Cancelled after {sw.ElapsedMilliseconds}ms.");
                source.Cancel();
            }
           
            /*
            _ = Task.Factory.StartNew(() => 
            {
                Console.WriteLine("Press ENTER if you want the program terminated");
                while (Console.ReadKey().Key != ConsoleKey.Enter) { }
                Console.WriteLine("Cancelling...");
                source.Cancel();

            },
            TaskCreationOptions.LongRunning);*/
            

            var queue = new ConcurrentQueue<IReadOnlyList<YoloV4Result>>();
            IReadOnlyList<YoloV4Result> list;


            var analizeTask = ImageAnalizer.imagesAnalizer(imageFolder, source.Token, queue);

            var dequeueTask = Task.Factory.StartNew(() =>
            {
                while ((!analizeTask.IsCompleted) && (!analizeTask.IsCanceled))
                {
                    if (!source.Token.IsCancellationRequested)
                    {
                        if (queue.TryDequeue(out list))
                        {
                            foreach (var value in list)
                            {
                                var x1 = value.BBox[0];
                                var y1 = value.BBox[1];
                                var x2 = value.BBox[2];
                                var y2 = value.BBox[3];
                                Console.WriteLine($"In a rectangle [left,top,right,bottom]:[{x1.ToString("F1", CultureInfo.InvariantCulture)}; " +
                                   $"{y1.ToString("F1", CultureInfo.InvariantCulture)}; {x2.ToString("F1", CultureInfo.InvariantCulture)}; " +
                                   $"{y2.ToString("F1", CultureInfo.InvariantCulture)}] was/were found {value.Label}.");
                            }
                        }
                        else Thread.Sleep(0);
                    }
                }
            },
            TaskCreationOptions.LongRunning);

            await Task.WhenAll(analizeTask, dequeueTask);
            
            sw.Stop();
            Console.WriteLine($"Done in {sw.ElapsedMilliseconds}ms.");
        }
    }
}
