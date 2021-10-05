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
            //Console.WriteLine("Paste full path to the directory with images that you wish to have analized");
            //var imageFolder = Console.ReadLine(); 

            await ImageAnalizer.imagesAnalizer(imageFolder);          
        }
    }
}
