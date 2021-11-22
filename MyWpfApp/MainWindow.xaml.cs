using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Win32;
using MyLibrary;
using System.Globalization;
using System.Collections.ObjectModel;
using System.IO;
using System.Drawing;

namespace MyWpfApp
{
    public class ObservableClasses : ObservableCollection<ClassObject> { }
    public class ClassObject
    {
        public string ClassName { get; set; }
        public override string ToString()
        {
            return ClassName;
        }
    }
    public class ObservableImages : ObservableCollection<ImageObject> { }
    public class ImageObject
    {
        public CroppedBitmap CroppedImage { get; set; }
        public string Name { get; set; }
        public string PredictedClass { get; set; }
    }
    public partial class MainWindow : Window
    {
        string imageFolder = "";
        CancellationTokenSource source = new CancellationTokenSource();

        public MainWindow()
        {
            InitializeComponent();
        }
        
        private void SelectButtonClicked(object sender, RoutedEventArgs e)
        {
            select_calalog_button.Background = System.Windows.Media.Brushes.Pink;
                    
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            dialog.InitialDirectory = "C:\\Users";
            dialog.IsFolderPicker = true;
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                imageFolder = dialog.FileName;
                MessageBox.Show("You selected: " + dialog.FileName);
            }
        }
    

        private void CancelButtonClicked(object sender, RoutedEventArgs e)
        {
            cancel_button.Background = System.Windows.Media.Brushes.Pink;
            source.Cancel();
            source = new CancellationTokenSource();
        }

        private void StartButtonClicked(object sender, RoutedEventArgs e)
        {
            start_button.Background = System.Windows.Media.Brushes.Pink;
            if (imageFolder == "") return;
            var queue = new ConcurrentQueue<Tuple<string,IReadOnlyList<YoloV4Result>>>();
            var analizeTask = ImageAnalizer.imagesAnalizer(imageFolder, source.Token, queue);

            var dequeueTask = Task.Factory.StartNew(async() =>
            {
                while ((!analizeTask.IsCompleted) && (!analizeTask.IsCanceled))
                {
                    if (!source.Token.IsCancellationRequested)
                    {
                        if (queue.TryDequeue(out Tuple<string, IReadOnlyList<YoloV4Result>> tuple))
                        {
                            var images = (FindResource("key_ObservableImages") as ObservableImages);
                            var classes = (FindResource("key_ObservableClasses") as ObservableClasses);
                            var imagesView = (FindResource("key_FilteredView") as CollectionViewSource);
                            
                            foreach (var value in tuple.Item2)
                            {
                                await Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    var x1 = (int)value.BBox[0];
                                    var y1 = (int)value.BBox[1];
                                    var x2 = (int)value.BBox[2];
                                    var y2 = (int)value.BBox[3];
                                    var UriSource = new Uri(tuple.Item1, UriKind.Relative);
                                    var newImage = new BitmapImage(UriSource);
                                    newImage.Freeze();
                                    var image = new CroppedBitmap(newImage, new Int32Rect(x1, y1, x2 - x1, y2 - y1));
                                    image.Freeze();
                                    images.Add(new ImageObject()
                                    {
                                        CroppedImage = image,
                                        PredictedClass = value.Label,
                                        Name = tuple.Item1
                                    });
                                    
                                    var itemToUpdate = images.FirstOrDefault(i => i.Name == tuple.Item1);
                                    if (itemToUpdate != null)
                                    {
                                        itemToUpdate.PredictedClass = value.Label;
                                        imagesView.View.Refresh();
                                    }
                                    var classToUpdate = classes.FirstOrDefault(i => i.ClassName == value.Label);
                                    if (classToUpdate != null)
                                    {
                                        classesList.Items.Refresh();
                                    }
                                    else
                                    {
                                        classes.Add(new ClassObject() { ClassName = value.Label});
                                    }
                                }));
                            }
                        }
                        else Thread.Sleep(0);
                    }
                }
            },
            TaskCreationOptions.LongRunning);
        }

        private void classesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            (FindResource("key_FilteredView") as CollectionViewSource).View.Refresh();
        }

        private void CollectionViewsource_Filter(object sender, FilterEventArgs e)
        {
            if (e.Item != null && classesList != null)
            {
                if (classesList.SelectedItem is null)
                {
                    e.Accepted = true;
                    return;
                }
                var selectedClass = (classesList.SelectedItem as ClassObject).ClassName;
                var imageClass = (e.Item as ImageObject).PredictedClass;
                if (selectedClass == imageClass)
                    e.Accepted = true;
                else e.Accepted=false;
            }
            else
            {
                e.Accepted = false;
            }
        }
    }
}
