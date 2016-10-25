using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Reflection;
using System.IO;
using Microsoft.Win32;
using System.Threading;

namespace FolderSplit
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region variables
        int maxWindowWidth = 700, maxWindowHeight=700, minWindowWidth = 500, minWindowHeight = 200;
        const String NA = "N/A";
        BackgroundWorker worker;
        #endregion

        #region delegates
        public delegate void UpdateProgressbar(int percentage);
        #endregion

        public MainWindow()
        {
            InitializeComponent();
            LoadAtStart();

            /*
            worker = new BackgroundWorker();
            worker.RunWorkerCompleted += Worker_WorkCompleted;
            worker.WorkerReportsProgress = true;
            worker.DoWork += Worker_DoWork;
            worker.ProgressChanged += Worker_ProgressChanged;
            */
        }

        private void TB_SourceFolder_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (Directory.Exists(TB_SourceFolder.Text))
            {
                DirectoryInfo chosenDirectory = new DirectoryInfo(TB_SourceFolder.Text);
                Label_FileNum.Content = chosenDirectory.EnumerateFiles().Count().ToString();
            }
            else {
                Label_FileNum.Content = NA;
            }
        }

        /// <summary>
        /// Operations to be completed with the Browser button for the Source Folder Path is clicked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_Browse_Click(object sender, RoutedEventArgs e)
        {
            //Disable Main Window
            MainCanvas.IsEnabled = false;

            FolderBrowserDialog folderDialog = new FolderBrowserDialog();
            DialogResult fileChosen = folderDialog.ShowDialog();
            if (!string.IsNullOrWhiteSpace(folderDialog.SelectedPath)) {
                TB_SourceFolder.Text = folderDialog.SelectedPath;
            }

            //Enable Main Window
            MainCanvas.IsEnabled = true;
        }

        /// <summary>
        /// Operations to be completed with the Browser button for the Destination Folder Path is clicked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_Browse_Dest_Click(object sender, RoutedEventArgs e)
        {
            //Disable Main Window
            MainCanvas.IsEnabled = false;

            FolderBrowserDialog folderDialog = new FolderBrowserDialog();
            DialogResult fileChosen = folderDialog.ShowDialog();
            if (!string.IsNullOrWhiteSpace(folderDialog.SelectedPath))
            {
                TB_DestinationFolder.Text = folderDialog.SelectedPath;
            }

            //Enable Main Window
            MainCanvas.IsEnabled = true;
        }

        /// <summary>
        /// Operations to be complete when the split operation is initiated by the user
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_Split_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                TextBlock_Status.Text = "";
                PBar_Status.Value = PBar_Status.Minimum;

                #region Check that all required parameters are set
                if (!Directory.Exists(TB_SourceFolder.Text.Trim()))
                {
                    TextBlock_Status.Text = "Soruce Folder Does Not Exist!";
                    return;
                }

                if (!Directory.Exists(TB_DestinationFolder.Text.Trim()))
                {
                    TextBlock_Status.Text = "Destination Folder Does Not Exist!";
                    return;
                }

                if (string.IsNullOrWhiteSpace(TB_BaseFolderName.Text.Trim()))
                {
                    TextBlock_Status.Text = "Base Folder Name is Empty. Please Fill it out!";
                    return;
                }

                int maxNumOfFilesPerFolder;
                bool intParsing = Int32.TryParse(TB_MaxFiles.Text, out maxNumOfFilesPerFolder);
                if (false == intParsing) {
                    TextBlock_Status.Text = "Please Enter an Integer for the Max Number of files you want per folder!";
                    return;
                }
                #endregion

                FolderSpiltInfo folderSplitInfo = new FolderSpiltInfo();
                folderSplitInfo.SourceDirectory = TB_SourceFolder.Text;
                folderSplitInfo.DestinationDirectory = TB_DestinationFolder.Text;
                folderSplitInfo.MaxNumOfFilePerFolder = maxNumOfFilesPerFolder;
                folderSplitInfo.BaseFolderName = TB_BaseFolderName.Text;

                //worker.RunWorkerAsync(folderSplitInfo);            

                ThreadPool.QueueUserWorkItem(new WaitCallback(ThreadPoolWork), folderSplitInfo);
            }
            catch (Exception exception)
            {

                TextBlock_Status.Text = "Error:\n"+exception;
            }

        }
                
        public void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            FolderSpiltInfo folderSplitInfo = e.Argument as FolderSpiltInfo;
            
            #region Get List of Files, and calculate variables in Shantel's Algorithm
            DirectoryInfo chosenDirectory = new DirectoryInfo(folderSplitInfo.SourceDirectory);
            double FilesFound = chosenDirectory.EnumerateFiles().Count(), FoldersNeeded = Math.Ceiling(FilesFound /((double)folderSplitInfo.MaxNumOfFilePerFolder));
            List<FileInfo> listOfFiles = chosenDirectory.EnumerateFiles().ToList()
                .Where(b => !b.Attributes.HasFlag(FileAttributes.Hidden))
                .OrderBy(c => c.FullName).ToList();

            int x = (int)FilesFound, y = folderSplitInfo.MaxNumOfFilePerFolder, z = (int)Math.Ceiling(FilesFound / FoldersNeeded);
            double zUnits = ((double)z) / 8.0;
            #endregion

            #region File Movement
            string folderName = "", sourceFilePath = "", destinationFilePath = "";
            for (int fileCounter = 1; fileCounter <= listOfFiles.Count(); fileCounter++)
            {
                //Create new Folder Name
                folderName = String.Format("{0}_{1}", folderSplitInfo.BaseFolderName, fileCounter);

                //Proceed, Only if this location we want to create does not exist
                if (!Directory.Exists(folderName))
                {
                    string absoluteDestinationFolderPath = System.IO.Path.Combine(folderSplitInfo.DestinationDirectory, folderName);

                    //Create New File Location and copy over some of the files over to it. 
                    Directory.CreateDirectory(absoluteDestinationFolderPath);
                    for (int numOfFilesInThisFolder = 1; numOfFilesInThisFolder <= folderSplitInfo.MaxNumOfFilePerFolder && fileCounter <= FilesFound; numOfFilesInThisFolder++, fileCounter++)
                    {
                        sourceFilePath = System.IO.Path.Combine(listOfFiles.ToArray()[fileCounter - 1].FullName);
                        destinationFilePath = System.IO.Path.Combine(folderSplitInfo.DestinationDirectory, folderName);
                        destinationFilePath = System.IO.Path.Combine(destinationFilePath, listOfFiles.ToArray()[fileCounter - 1].Name);
                        File.Copy(sourceFilePath, destinationFilePath);

                        //Report Progress
                        worker.ReportProgress((int)((fileCounter / FilesFound)*100));                        
                    }
                    fileCounter--;
                }
            }
            #endregion
        }

        public void Worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            PBar_Status.Value = PBar_Status.Maximum * (((double)e.ProgressPercentage)/100.00);
            //TextBlock_Status.Text = PBar_Status.Value.ToString();
        }

        public void Worker_WorkCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            TextBlock_Status.Text = "DONE!";
        }

        /// <summary>
        /// Work to be done by a thread pool thread
        /// </summary>
        /// <param name="o"></param>
        public void ThreadPoolWork(object o) {
            FolderSpiltInfo folderSplitInfo = o as FolderSpiltInfo;

            #region Get List of Files, and calculate variables in Shantel's Algorithm
            DirectoryInfo chosenDirectory = new DirectoryInfo(folderSplitInfo.SourceDirectory);
            double FilesFound = chosenDirectory.EnumerateFiles().Count(), FoldersNeeded = Math.Ceiling(FilesFound / ((double)folderSplitInfo.MaxNumOfFilePerFolder));
            List<FileInfo> listOfFiles = chosenDirectory.EnumerateFiles().ToList()
                .Where(b => !b.Attributes.HasFlag(FileAttributes.Hidden))
                .OrderBy(c => c.FullName).ToList();

            int x = (int)FilesFound, y = folderSplitInfo.MaxNumOfFilePerFolder, z = (int)Math.Ceiling(FilesFound / FoldersNeeded);
            double zUnits = ((double)z) / 8.0;
            #endregion

            #region File Movement
            string folderName = "", sourceFilePath = "", destinationFilePath = "";
            for (int fileCounter = 1; fileCounter <= listOfFiles.Count(); fileCounter++)
            {
                //Create new Folder Name
                folderName = String.Format("{0}_{1}", folderSplitInfo.BaseFolderName, fileCounter);

                //Proceed, Only if this location we want to create does not exist
                if (!Directory.Exists(folderName))
                {
                    string absoluteDestinationFolderPath = System.IO.Path.Combine(folderSplitInfo.DestinationDirectory, folderName);

                    //Create New File Location and copy over some of the files over to it. 
                    Directory.CreateDirectory(absoluteDestinationFolderPath);
                    for (int numOfFilesInThisFolder = 1; numOfFilesInThisFolder <= folderSplitInfo.MaxNumOfFilePerFolder && fileCounter <= FilesFound; numOfFilesInThisFolder++, fileCounter++)
                    {
                        sourceFilePath = System.IO.Path.Combine(listOfFiles.ToArray()[fileCounter - 1].FullName);
                        destinationFilePath = System.IO.Path.Combine(folderSplitInfo.DestinationDirectory, folderName);
                        destinationFilePath = System.IO.Path.Combine(destinationFilePath, listOfFiles.ToArray()[fileCounter - 1].Name);
                        File.Copy(sourceFilePath, destinationFilePath);

                        //Report Progress-> Update UI by Queuing up work on that thread.
                        this.PBar_Status.Dispatcher.Invoke(new UpdateProgressbar(this.NewProgressBarCompletionPercentage), (int)((fileCounter / FilesFound) * 100));
                    }
                    fileCounter--;
                }
            }
            #endregion
        }

        /// <summary>
        /// Updates Progress Bar to Reflect a new Percentage of Completion
        /// </summary>
        /// <param name="percentage"> Percentage the of the Progress Bar that should be filled</param>
        public void NewProgressBarCompletionPercentage(int percentage) {
            PBar_Status.Value = PBar_Status.Maximum * (((double)percentage) / 100.00);
        }

        /// <summary>
        /// Customs things that are loaded at the startup of the Main Window
        /// </summary>
        public void LoadAtStart() {
            this.Title = String.Format("{0} Version: {1}",this.Title, Assembly.GetExecutingAssembly().GetName().Version);
        }

        /// <summary>
        /// Contains Operations to be completed when the Main Window is Resized
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainWindow1_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            #region Make sure the Main Window Does Not Go Beyond Size Constraints
            if (this.Width >= this.maxWindowWidth)
                this.Width = this.maxWindowWidth;
            if (this.Height >= this.maxWindowHeight)
                this.Height = this.maxWindowHeight;
            if (this.Width <= this.minWindowWidth)
                this.Width = this.minWindowWidth;
            if (this.Height <= this.minWindowHeight)
                this.Height = this.minWindowHeight;
            #endregion

            #region Make sure Canvas Extends and Contracts with the Main Window
            MainCanvas.Width = this.Width;
            MainCanvas.Height = this.Height;
            #endregion


        }

        /// <summary>
        /// Increments Progress bar by 10 Percent
        /// </summary>
        private void IncrementProgressBar10Percent() {
            PBar_Status.Value = PBar_Status.Value + (PBar_Status.Maximum / 10);
        }

    }

    public class FolderSpiltInfo
    {
        public string SourceDirectory { get; set; }
        public string DestinationDirectory { get; set; }
        public string BaseFolderName { get; set; }
        public int MaxNumOfFilePerFolder { get; set; }
        
        public FolderSpiltInfo() { }

    }
}
