using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SystemSpkz.Utils;
using Microsoft.Win32;
using Newtonsoft.Json;
using static System.Convert;

namespace SystemSpkz
{
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        //Variables to be used on system architecture detection
        private const string Bits64 = "64-bit";
        private const string Bits32 = "32-bit";

        /**
         * Variable to save Imgur's URL if an image has been uploaded already to save HTTP calls 
         * and avoid captcha pages
         **/
        private string _imgurUrl;

        public MainWindow()
        {
            InitializeComponent();

            ProgressLabel.Content = "";
            ProgressLabel.Visibility = Visibility.Hidden;
            GeneralProgressBar.Visibility = Visibility.Hidden;
            ImgurDataGrid.Visibility = Visibility.Hidden;

            GetSpecs();

            ClipboardContentTxtBlck.Text = GenerateSpecsText();
        }

        /// <summary>
        /// Fills labels with info from methods
        /// </summary>
        private void GetSpecs()
        {
            OsInfoLabel.Content = GetOsInfo();
            CpuInfoLabel.Content = GetCpuInfo();
            RamInfoLabel.Content = ProcessInfoList(GetMemoryInfo());
            MoBoInfoLabel.Content = GetMoBoInfo();
            VideoInfoLabel.Content = ProcessInfoList(GetVideoInfo());
            StorageInfoLabel.Content = ProcessInfoList(GetStorageInfo());
            OpticalInfoLabel.Content = ProcessInfoList(GetOpticalDrivesInfo());
            AudioInfoLabel.Content = ProcessInfoList(GetAudioInfo());
            NetworkInfoLabel.Content = ProcessInfoList(GetNetworkInfo());
        }

        /// <summary>
        /// Returns a ManagementObjectCollection object containing a WMI info instance from the supplied id
        /// </summary>
        /// <param name="id">WMI class name</param>
        /// <returns>Returns an instance of supplied WMI class</returns>
        public ManagementObjectCollection GetManagementClassProperties(string id)
        {
            return new ManagementClass(id).GetInstances();
        }

        /// <summary>
        /// Builds a string to populate ClipboardContentTxtBlck content to be copied to the clipboard when the user clicks CopySpecsButton
        /// </summary>
        /// <returns>A formatted string with PC specs info</returns>
        private string GenerateSpecsText()
        {
            var sb = new StringBuilder();

            sb.AppendLine("CPU");
            ProcessItem(GetCpuInfo(), ref sb);
            sb.AppendLine();

            sb.AppendLine("GPU");
            ItemizeList(GetVideoInfo(), ref sb);
            sb.AppendLine();

            sb.AppendLine("RAM");
            ItemizeList(GetMemoryInfo(), ref sb);
            sb.AppendLine();

            sb.AppendLine("Operating system");
            ProcessItem(GetOsInfo(), ref sb);
            sb.AppendLine();

            sb.AppendLine("Motherboard");
            ProcessItem(GetMoBoInfo(), ref sb);
            sb.AppendLine();

            sb.AppendLine("Storage");
            ItemizeList(GetStorageInfo(), ref sb);
            sb.AppendLine();

            sb.AppendLine("Optical drives");
            ItemizeList(GetOpticalDrivesInfo(), ref sb);
            sb.AppendLine();

            sb.AppendLine("Audio");
            ItemizeList(GetAudioInfo(), ref sb);
            sb.AppendLine();

            sb.AppendLine("Network");
            ItemizeList(GetNetworkInfo(), ref sb);
            sb.AppendLine();

            return sb.ToString();
        }

        /// <summary>
        /// Formats a string as an item on a list
        /// </summary>
        /// <param name="s">Data string to include</param>
        /// <param name="sb">Current StringBuilder containing PC specs info</param>
        private void ProcessItem(string s, ref StringBuilder sb)
        {
            sb.AppendFormat($"\t- {s}\n");
        }

        /// <summary>
        /// Formats a list object into a list text 
        /// </summary>
        /// <param name="list">Data list to include</param>
        /// <param name="sb">Current StringBuilder containing PC specs info</param>
        private void ItemizeList(List<string> list, ref StringBuilder sb)
        {
            foreach (var s in list)
                ProcessItem(s, ref sb);
        }

        /// <summary>
        /// Cleans up a string from unwanted characters ny trimming it
        /// </summary>
        /// <param name="info">String to be cleaned up</param>
        /// <returns>Returns a clean string</returns>
        private string CleanUpInfoString(string info)
        {
            return info
                .TrimEnd();
        }

        /// <summary>
        /// Formats a list into several lines
        /// </summary>
        /// <param name="info">String list containing a component specs</param>
        /// <returns>Returns a formatted string</returns>
        private string ProcessInfoList(List<string> info)
        {
            return CleanUpInfoString(string.Join(Environment.NewLine, info));
        }

        /// <summary>
        /// Gets info about network devices used in the computer
        /// </summary>
        /// <returns>Returns a list of networking devices</returns>
        private List<string> GetNetworkInfo()
        {
            var info = new List<string>();

            foreach (var mgmtObj in GetManagementClassProperties("Win32_NetworkAdapter"))
                if (mgmtObj["NetConnectionStatus"] != null && mgmtObj["PhysicalAdapter"] != null)
                    if (mgmtObj["NetConnectionStatus"].ToString() == "2" && ToBoolean(mgmtObj["PhysicalAdapter"]))
                        info.Add($"{mgmtObj["Description"]}");

            return info;
        }

        /// <summary>
        /// Gets info about audio devices used in the computer
        /// </summary>
        /// <returns>Returns a list of audio devices</returns>
        private List<string> GetAudioInfo()
        {
            var info = new List<string>();

            foreach (var mgmtObj in GetManagementClassProperties("Win32_SoundDevice"))
                info.Add($"{mgmtObj["Caption"]}");

            return info;
        }

        /// <summary>
        /// Gets info about optical drives used in the computer
        /// </summary>
        /// <returns>Returns a list of optical drives</returns>
        private List<string> GetOpticalDrivesInfo()
        {
            var info = new List<string>();

            foreach (var mgmtObj in GetManagementClassProperties("Win32_CDROMDrive"))
                info.Add($"{mgmtObj["Caption"]}");

            return info;
        }

        /// <summary>
        /// Gets info about storage units used in the computer
        /// </summary>
        /// <returns>Returns a list of storage units</returns>
        private List<string> GetStorageInfo()
        {
            var info = new List<string>();

            foreach (var mgmtObj in GetManagementClassProperties("Win32_DiskDrive"))
            {
                var storageAmount = Math.Ceiling((double) ToInt64(mgmtObj["Size"]) / 1073741824);
                info.Add($"{mgmtObj["Caption"]} {storageAmount} GB");
            }

            return info;
        }

        /// <summary>
        /// Gets info about GPU(s) used in the computer
        /// </summary>
        /// <returns>Returns a list of GPUs</returns>
        private List<string> GetVideoInfo()
        {
            var info = new List<string>();

            foreach (var mgmtObj in GetManagementClassProperties("Win32_VideoController"))
            {
                var vramAmount = Math.Ceiling((double) ToInt64(mgmtObj["AdapterRAM"]) / 1073741824);
                info.Add($"{mgmtObj["Caption"]} {vramAmount} GB");
            }

            return info;
        }

        /// <summary>
        /// Gets info about computer's motherboard
        /// </summary>
        /// <returns>Returns motherboard's info</returns>
        private string GetMoBoInfo()
        {
            var cpuSocket = string.Empty;
            var info = string.Empty;

            foreach (var mgmtObj in GetManagementClassProperties("Win32_Processor"))
                cpuSocket = mgmtObj["SocketDesignation"].ToString();

            foreach (var mgmtObj in GetManagementClassProperties("Win32_BaseBoard"))
                info = $"{mgmtObj["Manufacturer"]} {mgmtObj["Product"]} ({cpuSocket})";

            return CleanUpInfoString(info);
        }

        /// <summary>
        /// Gets info about RAM sticks used in the computer
        /// </summary>
        /// <returns>Returns a list of RAM sticks</returns>
        private List<string> GetMemoryInfo()
        {
            var info = new List<string>();

            foreach (var mgmtObj in GetManagementClassProperties("Win32_PhysicalMemory"))
                info.Add(
                    $"{ToInt64(mgmtObj["Capacity"]) / 1073741824} GB {mgmtObj["Manufacturer"]} @ {mgmtObj["Speed"]} MHz");

            return info;
        }

        /// <summary>
        /// Gets info about computer's CPU
        /// </summary>
        /// <returns>Returns CPU's info</returns>
        private string GetCpuInfo()
        {
            var info = string.Empty;

            foreach (var mgmtObj in GetManagementClassProperties("Win32_Processor"))
                info = $"{mgmtObj["Name"]}";

            return CleanUpInfoString(info);
        }

        /// <summary>
        /// Gets info about computer's OS
        /// </summary>
        /// <returns>Returns OS' info</returns>
        private string GetOsInfo()
        {
            var info = string.Empty;

            foreach (var mgmtObj in GetManagementClassProperties("Win32_OperatingSystem"))
            {
                var architecture = Environment.Is64BitOperatingSystem ? Bits64 : Bits32;
                info = $"{mgmtObj["Caption"]} {architecture}";
            }

            return CleanUpInfoString(info);
        }

        /// <summary>
        /// CreatePngImageButton_Click event handler
        /// </summary>
        private void CreatePngImageButton_Click(object sender, RoutedEventArgs e)
        {
            CreateImage(false);
        }

        /// <summary>
        /// Create an image based on DataWrapper's grid content.
        /// </summary>
        /// <param name="isTempImg">Sets if the image will be temporary (for Imgur/social uploads).</param>
        /// <returns>Returns image's path</returns>
        private string CreateImage(bool isTempImg)
        {
            var path = string.Empty;

            //If isTempImg == false then show an open file dialog and set the path to user's selected path
            if (!isTempImg)
            {
                var openDialog = new SaveFileDialog
                {
                    Filter = "Image files (*.png) | *.png;"
                };

                if (openDialog.ShowDialog() == true)
                    path = openDialog.FileName;
            }
            //Otherwise set the name to temp.png and path to app current directory
            else
            {
                path = Path.Combine(Environment.CurrentDirectory, "temp.png");
            }

            //Generate the image
            if (!string.IsNullOrEmpty(path))
            {
                var height = (int) DataWrapper.ActualHeight;
                var width = (int) DataWrapper.ActualWidth;

                var bmp = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                bmp.Render(DataWrapper);

                BitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bmp));

                using (var stm = File.Create(path))
                {
                    encoder.Save(stm);
                }
            }

            return path;
        }

        /// <summary>
        /// UploadImageButton_Click event handler.
        /// </summary>
        private async void UploadImageButton_Click(object sender, RoutedEventArgs e)
        {
            await UploadImage(false);
        }

        /// <summary>
        /// Upload image to Imgur.
        /// </summary>
        /// <param name="isSocial">Sets if the string is for a social network post.</param>
        /// <returns>A Task object to be used in async methods</returns>
        private async Task UploadImage(bool isSocial)
        {
            //If string.IsNullOrEmpty(_imgurUrl) == true it means that user hasn't uploaded any images while current program's execution
            if (string.IsNullOrEmpty(_imgurUrl))
            {
                var cookies = GetCookies();
                var imgurData = new ImgurAlbumData();
                var imgurImageData = new ImgurImageData();
                var errorMessage = string.Empty;

                ProgressLabel.Content = "Checking captcha";
                ProgressLabel.Visibility = Visibility.Visible;
                GeneralProgressBar.Visibility = Visibility.Visible;

                //Check if imgur asks for captcha first
                if (!checkCaptcha(cookies, ref imgurData, ref errorMessage))
                {
                    MessageBox.Show(errorMessage, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    //We have an OK and album data from imgur to upload images
                    if (imgurData.Success)
                    {
                        //Generate image
                        ProgressLabel.Content = "Generating image";
                        var file = new FileInfo(CreateImage(true));

                        if (File.Exists(file.FullName))
                        {
                            ProgressLabel.Content = "Uploading image";

                            //Upload image using as a multipart form
                            var task = Task.Factory.StartNew(() =>
                            {
                                using (var fs = new FileStream(file.FullName, FileMode.Open, FileAccess.Read))
                                {
                                    var data = new byte[fs.Length];
                                    fs.Read(data, 0, data.Length);

                                    var postParameters = new Dictionary<string, object>
                                    {
                                        {"new_album_id", imgurData.Data.NewAlbumId},
                                        {
                                            "file",
                                            new FormUpload.FileParameter(data, file.Name,
                                                MimeMapping.GetMimeMapping(file.FullName))
                                        }
                                    };

                                    using (var s = FormUpload.MultipartFormDataPost("http://imgur.com/upload",
                                            "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/56.0.2924.87 Safari/537.36",
                                            postParameters, cookies)
                                        .GetResponseStream())
                                    {
                                        if (s != null)
                                            using (var sr = new StreamReader(s, Encoding.UTF8))
                                            {
                                                imgurImageData =
                                                    JsonConvert.DeserializeObject<ImgurImageData>(sr.ReadToEnd());
                                            }
                                    }
                                }
                            });

                            //Wait for task completion
                            await task;

                            //If it isn't for a social site, show a textbox to allow the user to copy imgur's URL
                            if (!isSocial)
                            {
                                ImgurDataGrid.Visibility = Visibility.Visible;
                                ImgurUrlTxtBx.Text = $"http://imgur.com/{imgurImageData.Data.Hash}";
                                _imgurUrl = ImgurUrlTxtBx.Text;
                            }
                            //Otherwise store it for future use (to avoid unneeded HTTP calls)
                            else
                            {
                                ImgurUrlTxtBx.Text = $"http://imgur.com/{imgurImageData.Data.Hash}";
                                _imgurUrl = ImgurUrlTxtBx.Text;
                            }

                            File.Delete(file.FullName);
                        }
                        else
                        {
                            MessageBox.Show("File does not exist on the specified directory!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    else
                    {
                        MessageBox.Show("Try again later!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }

                ProgressLabel.Content = "";
                ProgressLabel.Visibility = Visibility.Hidden;
                GeneralProgressBar.Visibility = Visibility.Hidden;
            }
            else
            {
                //Just show a textbox to allow the user to copy imgur's URL
                if (!isSocial)
                    ImgurDataGrid.Visibility = Visibility.Visible;
            }
        }

        /// <summary>
        /// Generate a pair of cookies based on http://s.imgur.com/include/js/jafo.js cookie generation's procedure reverse engineering
        /// </summary>
        /// <returns></returns>
        private CookieContainer GetCookies()
        {
            var cookie1 = new Cookie();
            var cookie2 = new Cookie();
            var cookies = new CookieContainer();

            cookie1.Path = "/";
            cookie1.Domain = ".imgur.com";
            cookie1.Name = "IMGURUIDJAFO";
            cookie1.Value = GetCookieHash();
            cookie1.Expires = DateTime.Now.AddDays(180);

            cookie2.Path = "/";
            cookie2.Domain = ".imgur.com";
            cookie2.Name = "SESSIONDATA";
            cookie2.Value =
                HttpUtility.UrlEncode($"{{\"sessionCount\":1,\"sessionTime\":{ToUnixTimestamp(DateTime.Now)}}}");
            cookie2.Expires = DateTime.Now.AddDays(180);

            cookies.Add(cookie1);
            cookies.Add(cookie2);

            return cookies;
        }

        /// <summary>
        /// Convert a datetime to UNIX timestamp
        /// </summary>
        /// <param name="target">Target datetime</param>
        /// <returns>Returns a UNIX timestamp</returns>
        public long ToUnixTimestamp(DateTime target)
        {
            var date = new DateTime(1970, 1, 1, 0, 0, 0, target.Kind);
            var unixTimestamp = ToInt64((target - date).TotalSeconds);

            return unixTimestamp;
        }

        /// <summary>
        /// Computes a SHA256 hash with a JS navigator object info
        /// </summary>
        /// <returns>SHA 256 hash of navigator info</returns>
        private string GetCookieHash()
        {
            var sb = new StringBuilder();

            using (var sha = SHA256.Create())
            {
                var enc = Encoding.UTF8;
                var result = sha.ComputeHash(
                    enc.GetBytes(
                        "20030107Google Inc.08MozillaNetscape5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537.36Win32GeckoMozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537.36estruetruenullundefinedundefinedundefinedundefinedundefined2017 - 05 - 23T05: 50:13.522Z"));

                foreach (var b in result)
                    sb.Append(b.ToString("x2"));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Connects imgur's upload/checkcaptcha endpoint to check if a captcha has to be solved before uploading an image, will return false if a captcha has to be solved or if any exceptions ocurred 
        /// </summary>
        /// <param name="cookies">Cookies needed to complete the request</param>
        /// <param name="imgurData">Imgur album data to be collected from the endpoint if no captcha is required</param>
        /// <param name="errorMessage">Error message to be reported to the user if any exceptions were thrown</param>
        /// <returns></returns>
        private bool checkCaptcha(CookieContainer cookies, ref ImgurAlbumData imgurData, ref string errorMessage)
        {
            const string captchaUrl = "http://imgur.com/upload/checkcaptcha";
            var data = new ImgurAlbumData();

            try
            {
                var task = Task.Factory.StartNew(() =>
                {
                    var captchaRequest = WebRequest.Create(captchaUrl) as HttpWebRequest;

                    if (captchaRequest != null)
                    {
                        var captchaPostString = "total_uploads=1&create_album=true";
                        captchaRequest.ContentType = "application/x-www-form-urlencoded; charset=UTF-8";
                        captchaRequest.Method = "POST";
                        captchaRequest.CookieContainer = cookies;
                        captchaRequest.Accept = "*/*";
                        captchaRequest.AutomaticDecompression = DecompressionMethods.GZip |
                                                                DecompressionMethods.Deflate;
                        captchaRequest.UserAgent =
                            "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/56.0.2924.87 Safari/537.36";
                        captchaRequest.Referer = "http://imgur.com";
                        captchaRequest.KeepAlive = true;

                        var captchaCustomHeaders = captchaRequest.Headers;

                        captchaCustomHeaders.Add("Accept-Language", "en;q=0.8");
                        captchaCustomHeaders.Add("origin", "http://imgur.com");
                        captchaCustomHeaders.Add("x-requested-with", "XMLHttpRequest");

                        var captchaBytes = Encoding.ASCII.GetBytes(captchaPostString);

                        captchaRequest.ContentLength = captchaBytes.Length;

                        using (var os = captchaRequest.GetRequestStream())
                        {
                            os.Write(captchaBytes, 0, captchaBytes.Length);
                        }

                        var adS3Response = captchaRequest.GetResponse() as HttpWebResponse;


                        if (adS3Response != null && adS3Response.StatusCode == HttpStatusCode.OK)
                            using (var s = adS3Response.GetResponseStream())
                            {
                                if (s != null)
                                    using (
                                        // ReSharper disable once AssignNullToNotNullAttribute
                                        var sr = new StreamReader(s, Encoding.UTF8)
                                    )
                                    {
                                        data = JsonConvert.DeserializeObject<ImgurAlbumData>(sr.ReadToEnd());
                                    }
                            }
                    }
                });

                try
                {
                    task.Wait();
                }
                catch (AggregateException ae)
                {
                    if (ae.InnerException != null)
                        errorMessage = ae.InnerException.Message + Environment.NewLine + ae.StackTrace;
                    else
                        errorMessage = ae.Message + Environment.NewLine + ae.StackTrace;

                    ae.Handle(x => false);
                }
            }
            catch (Exception e)
            {
                if (e.InnerException != null)
                    errorMessage = e.InnerException.Message + Environment.NewLine + e.StackTrace;
                else
                    errorMessage = e.Message + Environment.NewLine + e.StackTrace;

                return false;
            }

            if (data == null)
                return false;

            imgurData = data;

            return imgurData.Success;
        }

        /// <summary>
        /// DismissImgurBtn_Click event handler.
        /// </summary>
        private void DismissImgurBtn_Click(object sender, RoutedEventArgs e)
        {
            ImgurDataGrid.Visibility = Visibility.Hidden;
        }

        /// <summary>
        /// CopySpecsButton_Click event handler.
        /// </summary>
        private void CopySpecsButton_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(ClipboardContentTxtBlck.Text);
            MessageBox.Show("Specs copied as text to Clipboard!", "Copied", MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        /// <summary>
        /// TwitterShareButton_Click event handler.
        /// </summary>
        private async void TwitterShareButton_Click(object sender, RoutedEventArgs e)
        {
            await UploadImage(true);

            var tweetUrl =
                $"https://twitter.com/intent/tweet?hashtags=PCGaming,CPUCores&text={HttpUtility.UrlEncode("Check out my PC specs! via CPUCores")}&url={ImgurUrlTxtBx.Text}";
            Process.Start(tweetUrl);
        }
    }

    /// <summary>
    /// Auxiliary class
    /// </summary>
    public class AlbumData
    {
        [JsonProperty("overlimits")]
        public int OverLimits { get; set; }

        [JsonProperty("upload_count")]
        public string UploadCount { get; set; }

        [JsonProperty("new_album_id")]
        public string NewAlbumId { get; set; }

        [JsonProperty("deletehash")]
        public string DeleteHash { get; set; }
    }

    /// <summary>
    /// Auxiliary class
    /// </summary>
    public class ImgurAlbumData
    {
        [JsonProperty("data")]
        public AlbumData Data { get; set; }

        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("status")]
        public int Status { get; set; }
    }

    /// <summary>
    /// Auxiliary class
    /// </summary>
    public class ImageData
    {
        [JsonProperty("hashes")]
        public List<string> Hashes { get; set; }

        [JsonProperty("hash")]
        public string Hash { get; set; }

        [JsonProperty("deletehash")]
        public string DeleteHash { get; set; }

        [JsonProperty("album")]
        public string Album { get; set; }

        [JsonProperty("edit")]
        public bool Edit { get; set; }

        [JsonProperty("gallery")]
        public object Gallery { get; set; }

        [JsonProperty("animated")]
        public bool Animated { get; set; }

        [JsonProperty("height")]
        public int Height { get; set; }

        [JsonProperty("width")]
        public int Width { get; set; }

        [JsonProperty("ext")]
        public string Ext { get; set; }

        [JsonProperty("msid")]
        public string Msid { get; set; }
    }

    /// <summary>
    /// Auxiliary class
    /// </summary>
    public class ImgurImageData
    {
        [JsonProperty("data")]
        public ImageData Data { get; set; }

        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("status")]
        public int Status { get; set; }
    }
}