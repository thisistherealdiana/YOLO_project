**The project is a console app .NET (Framework или Core) that processes the directory specified by the user and displays information about the objects found on the images.**

The program prints a list of objects found in the image as each object is processed. Recognition is performed asynchronously and results are generated as the data in the directory is processed. Multiple file recognition runs at the same time in order to maximize the use of all cores of all processors in the system. To implement multithreading and asynchrony,** TPL Flow **tools are being used.

Model used for image recognition is [https://github.com/onnx/models](https://github.com/onnx/models).

The user, in turn, is presented with a user interface application for selecting a directory of images and viewing the results. All recognized objects found on the images are available for selection by the user. When a certain object type is selected, the application window displays all images of objects of this type. The list of images is updated as the files are recognized.
