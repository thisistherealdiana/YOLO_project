**The project is a console app .NET (Framework или Core) that processes the directory specified by the user (in this repository that is a folder called Images) and displays information about the objects found on the images.**

The program prints a list of objects found in the image as each object is processed. Recognition is performed asynchronously and results are generated as the data in the directory is processed. Multiple file recognition runs at the same time in order to maximize the use of all cores of all processors in the system. To implement multithreading and asynchrony, **TPL Flow** tools are being used.

Model used for image recognition is [https://github.com/onnx/models](https://github.com/onnx/models).

The user, in turn, is presented with a user interface application for selecting a directory of images and viewing the results. All recognized objects found on the images are available for selection by the user. When a certain object type is selected, the application window displays all images of objects of this type. The list of images is updated as the files are recognized.

The results of the program are being saved to the database (that can be cleared by clicking a button on the user interface), that's why after first-time execution of the program there are preexisting images in the database. The database is managed using SQLite database engine. When database is not empty and after choosing another image folder, the database is going to update with newly found objects.

**The example of program execution can be seen in the folder Example.**
The video can be seen below

https://user-images.githubusercontent.com/74499545/195565011-e5620e59-d34e-4faa-b452-3a3770ff3537.mp4

