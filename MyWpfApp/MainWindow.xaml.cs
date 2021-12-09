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
using Microsoft.EntityFrameworkCore;

namespace MyWpfApp
{
    public class ObservableDatabase : ObservableCollection<DatabaseImageObject> { }
    public class DatabaseImageObject
    {
        public int Id { get; set; }
        public byte[] DatabaseImage { get; set; }
        public float x1 { get; set; }
        public float x2 { get; set; }
        public float y1 { get; set; }
        public float y2 { get; set; }
        public string DatabaseClass { get; set; }
    }
    //public class DatabaseClassObject
    //{
    //    public int Id { get; set; }
    //    //public string DatabaseClassName { get; set; }
    //    public List<DatabaseImageObject> Images { get; set; } = new List<DatabaseImageObject>();
    //}
    class ImagesContext : DbContext
    {
        public DbSet<DatabaseImageObject> Images {get;set;}
       
        public ImagesContext() :base()
        {
            Database.EnsureCreated();
        }
        protected override void OnConfiguring(DbContextOptionsBuilder o) => o.UseLazyLoadingProxies().UseSqlite("Data Source=C:\\Users\\archi\\OneDrive\\Рабочий стол\\dbfolder\\New_database.db");
    }

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
        ImagesContext db;

        public MainWindow()
        {
            InitializeComponent();
            select_calalog_button.IsEnabled = true;
            start_button.IsEnabled = false;
            cancel_button.IsEnabled = false;

            
            db = new ImagesContext();
        }

        private void SelectButtonClicked(object sender, RoutedEventArgs e)
        {
            select_calalog_button.Background = System.Windows.Media.Brushes.Pink;
            select_calalog_button.IsEnabled = false;
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            dialog.InitialDirectory = "C:\\Users";
            dialog.IsFolderPicker = true;
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                imageFolder = dialog.FileName;
                MessageBox.Show("You selected: " + dialog.FileName);
            }
            start_button.IsEnabled = true;
        }


        private void CancelButtonClicked(object sender, RoutedEventArgs e)
        {
            cancel_button.Background = System.Windows.Media.Brushes.Pink;
            source.Cancel();
            source = new CancellationTokenSource();
        }

        private byte[] ImageToByteArray(System.Drawing.Image img)
        {
            using (var stream = new MemoryStream())
            {
                img.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                return stream.ToArray();
            }
        }

        private void StartButtonClicked(object sender, RoutedEventArgs e)
        {
            start_button.Background = System.Windows.Media.Brushes.Pink;
            select_calalog_button.IsEnabled = false;
            start_button.IsEnabled = false;
            cancel_button.IsEnabled = true;
            if (imageFolder == "") return;
            var queue = new ConcurrentQueue<Tuple<string, IReadOnlyList<YoloV4Result>>>();
            var analizeTask = ImageAnalizer.imagesAnalizer(imageFolder, source.Token, queue);
            var dequeueTask = Task.Factory.StartNew(async () =>
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
                                    var rectangle = new System.Drawing.Rectangle(x1, y1, x2 - x1, y2 - y1);
                                    System.Drawing.Image imagee = System.Drawing.Image.FromFile(tuple.Item1);
                                    Bitmap bmpImage = new Bitmap(imagee);
                                    Bitmap croppedImage = bmpImage.Clone(rectangle, bmpImage.PixelFormat);
                                    byte[] blob = ImageToByteArray(croppedImage);
                                    if (!imageExistsInDatabase(x1, y1, x2, y2, blob))
                                    {
                                        var databaseImageObject = new DatabaseImageObject
                                        {
                                            DatabaseImage = blob,
                                            x1 = x1,
                                            x2 = x2,
                                            y1 = y1,
                                            y2 = y2,
                                            DatabaseClass = value.Label
                                        };
                                        db.Images.Add(databaseImageObject);
                                        db.SaveChanges();
                                    }

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
                                        classes.Add(new ClassObject() { ClassName = value.Label });
                                    }
                                }));
                            }
                        }
                        else Thread.Sleep(0);
                    }
                }
            },
            TaskCreationOptions.LongRunning);
            
            select_calalog_button.IsEnabled = true;
            start_button.IsEnabled = false;
            cancel_button.IsEnabled = false;
        }

        private bool imageExistsInDatabase(int x1, int y1, int x2, int y2, byte[] blob)
        {
            var query = db.Images.Where(item => item.x1 == x1 && item.y1 == y1 && item.x2 == x2 && item.y2 == y2).Select(item => item.DatabaseImage);
            foreach (byte[] item in query)
            {
                if (item.SequenceEqual(blob))
                {
                    return true;
                }
            }
            return false;
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
                else e.Accepted = false;
            }
            else
            {
                e.Accepted = false;
            }
        }

        private void ClearDatabaseClicked(object sender, RoutedEventArgs e)
        {
            cleardb_button.Background = System.Windows.Media.Brushes.Pink;
            foreach (var item in db.Images)
            {
                db.Images.Remove(item);
            }
            db.SaveChanges();
        }
    }
}
