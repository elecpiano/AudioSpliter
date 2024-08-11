using Microsoft.Win32;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

namespace AudioSpliter
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        string SourceAudioFile = "";
        string DestFolder = "";
        List<string> TimeList = new List<string>();
        List<string> DestFileList = new List<string>();

        public MainWindow()
        {
            InitializeComponent();
        }

        private void BtnSource_click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "*.mp3|*.*";
            ofd.Multiselect = false;
            if (ofd.ShowDialog() == true)
            {
                TxtSourceFile.Text = ofd.FileName;
                SourceAudioFile = ofd.FileName;
            }
        }

        private void BtnGo_click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(SourceAudioFile) || !File.Exists(SourceAudioFile))
            {
                MessageBox.Show("Select an audio file");
                return;
            }
            LoadAudioSource();
        }

        private async void LoadAudioSource()
        {
            TimeList = new List<string>();
            TimeList.Add("0");

            Process p = new Process();

            p.StartInfo.FileName = "ffmpeg.exe";
            p.StartInfo.Arguments = "-i 1.mp3 -af silencedetect=d=5.9 -f null -";
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardInput = true;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            p.ErrorDataReceived += new DataReceivedEventHandler(ProcessDataReceived);
            p.StartInfo.StandardErrorEncoding = Encoding.UTF8;

            p.Start();
            p.BeginErrorReadLine();
            await p.WaitForExitAsync();

            CheckDestFileList();

            p.Close();
            p.Dispose();
        }

        private void ProcessDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                if (e.Data.Contains("silence_start: "))
                {
                    string timeStr = e.Data.Substring(e.Data.IndexOf(":") + 2, e.Data.Length - e.Data.IndexOf(":") - 2);
                    TimeList.Add(timeStr);
                }
                else if (e.Data.Contains("silence_end: "))
                {
                    string timeStr = e.Data.Substring(e.Data.IndexOf(":") + 2, e.Data.IndexOf("|") - e.Data.IndexOf(":") - 3);
                    TimeList.Add(timeStr);
                }
                //File.WriteAllText("ffmpeg-output.txt", e.Data);
            }
        }

        private void CheckDestFileList()
        {
            DestFileList = new List<string>();
            string[] strArr = TxtDestFileList.Text.Split(System.Environment.NewLine);
            for (int i = 0; i < strArr.Length; i++)
            {
                DestFileList.Add(strArr[i]);
            }

            if (DestFileList.Count == (TimeList.Count -1)/2)
            {
                //SplitAudio();
                FileInfo fi = new FileInfo(SourceAudioFile);
                DestFolder = Path.Combine(fi.Directory.FullName, fi.Name.Remove(fi.Name.IndexOf(fi.Extension)));
                Directory.CreateDirectory(DestFolder);
                SplitAudioAsync();
            }
            else
            {
                MessageBox.Show("File count mismatch !");
            }
        }

        private void SplitAudio()
        {
            Process p = new Process();
            p.StartInfo.FileName = "ffmpeg.exe";
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardInput = true;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;

            int i = 0;
            while (i < DestFileList.Count)
            {
                string start = TimeList[2*i];
                string end = TimeList[2*i+1];
                SplitAudioUnit(p, start, end, DestFileList[i]);
                i++;
            }
            //SplitAudioUnit("38.778792", "48.819833");

            p.Close();
            p.Dispose();

            MessageBox.Show("Complete !");
        }

        private void SplitAudioUnit(Process p, string start, string end, string destFileName)
        {
            string fmtStr = "-ss {0} -t {1} -i {2} {3}";
            string param_0 = start;;
            string param_1 = (float.Parse(end) - float.Parse(start)).ToString();
            string param_2 = SourceAudioFile;
            string param_3 = destFileName + ".mp3";

            p.StartInfo.Arguments = string.Format(fmtStr, param_0, param_1, param_2, param_3);

            p.Start();
            p.WaitForExit();
        }

        private async void SplitAudioAsync()
        {
            Pbar.Maximum = DestFileList.Count;
            Pbar.Value = 0;

            Process p = new Process();

            p.StartInfo.FileName = "ffmpeg.exe";
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardInput = true;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;

            int i = 0;
            while (i < DestFileList.Count)
            {
                string start = TimeList[2 * i];
                string end = TimeList[2 * i + 1];
                await SplitAudioUnitAsync(p, start, end, DestFileList[i]);
                i++;
                Pbar.Dispatcher.Invoke(() => {
                    Pbar.Value = i;
                });
            }

            p.Close();
            p.Dispose();

            MessageBox.Show("Complete !");
        }


        private async Task SplitAudioUnitAsync(Process p, string start, string end, string destFileName)
        {
            string fmtStr = "-ss {0} -t {1} -i {2} {3}";
            string param_0 = start; ;
            string param_1 = (float.Parse(end) - float.Parse(start)).ToString();
            string param_2 = SourceAudioFile;
            string param_3 = Path.Combine(DestFolder, destFileName + ".mp3" );

            p.StartInfo.Arguments = string.Format(fmtStr, param_0, param_1, param_2, param_3);

            p.Start();
            await p.WaitForExitAsync();
        }

    }
}
