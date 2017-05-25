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
        private const string Bits64 = "64-bit";
        private const string Bits32 = "32-bit";

        private string _imgurUrl;

        public MainWindow()
        {
            InitializeComponent();

            ProgressLabel.Content = "";
            ProgressLabel.Visibility = Visibility.Hidden;
            GeneralProgressBar.Visibility = Visibility.Hidden;
            ImgurDataGrid.Visibility = Visibility.Hidden;

            OsInfoLabel.Content = GetOsInfo();
            CpuInfoLabel.Content = GetCpuInfo();
            RamInfoLabel.Content = ProcessInfoList(GetMemoryInfo());
            MoBoInfoLabel.Content = GetMoBoInfo();
            VideoInfoLabel.Content = ProcessInfoList(GetVideoInfo());
            StorageInfoLabel.Content = ProcessInfoList(GetStorageInfo());
            OpticalInfoLabel.Content = ProcessInfoList(GetOpticalDrivesInfo());
            AudioInfoLabel.Content = ProcessInfoList(GetAudioInfo());
            NetworkInfoLabel.Content = ProcessInfoList(GetNetworkInfo());

            ClipboardContentTxtBlck.Text = GenerateSpecsText();
        }

        public ManagementObjectCollection GetManagementClassProperties(string id)
        {
            return new ManagementClass(id).GetInstances();
        }

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

        private void ProcessItem(string s, ref StringBuilder sb)
        {
            sb.AppendFormat($"\t- {s}\n");
        }

        private void ItemizeList(List<string> list, ref StringBuilder sb)
        {
            foreach (var s in list)
                ProcessItem(s, ref sb);
        }

        private string CleanUpInfoString(string info)
        {
            return info
                .TrimEnd();
        }

        private string ProcessInfoList(List<string> info)
        {
            return CleanUpInfoString(string.Join(Environment.NewLine, info));
        }

        private List<string> GetNetworkInfo()
        {
            var info = new List<string>();

            foreach (var mgmtObj in GetManagementClassProperties("Win32_NetworkAdapter"))
                if (mgmtObj["NetConnectionStatus"] != null && mgmtObj["PhysicalAdapter"] != null)
                    if (mgmtObj["NetConnectionStatus"].ToString() == "2" && ToBoolean(mgmtObj["PhysicalAdapter"]))
                        info.Add($"{mgmtObj["Description"]}");

            return info;
        }

        private List<string> GetAudioInfo()
        {
            var info = new List<string>();

            foreach (var mgmtObj in GetManagementClassProperties("Win32_SoundDevice"))
                info.Add($"{mgmtObj["Caption"]}");

            return info;
        }

        private List<string> GetOpticalDrivesInfo()
        {
            var info = new List<string>();

            foreach (var mgmtObj in GetManagementClassProperties("Win32_CDROMDrive"))
                info.Add($"{mgmtObj["Caption"]}");

            return info;
        }

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

        private List<string> GetMemoryInfo()
        {
            var info = new List<string>();

            foreach (var mgmtObj in GetManagementClassProperties("Win32_PhysicalMemory"))
                info.Add(
                    $"{ToInt64(mgmtObj["Capacity"]) / 1073741824} GB {mgmtObj["Manufacturer"]} @ {mgmtObj["Speed"]} MHz");

            return info;
        }

        private string GetCpuInfo()
        {
            var info = string.Empty;

            foreach (var mgmtObj in GetManagementClassProperties("Win32_Processor"))
                info = $"{mgmtObj["Name"]}";

            return CleanUpInfoString(info);
        }

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

        private void CreatePngImageButton_Click(object sender, RoutedEventArgs e)
        {
            CreateImage(false);
        }

        private string CreateImage(bool isTempImg)
        {
            var path = string.Empty;

            if (!isTempImg)
            {
                var openDialog = new SaveFileDialog
                {
                    Filter = "Image files (*.png) | *.png;"
                };

                if (openDialog.ShowDialog() == true)
                    path = openDialog.FileName;
            }
            else
            {
                path = Path.Combine(Environment.CurrentDirectory, "temp.png");
            }

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

        private async void UploadImageButton_Click(object sender, RoutedEventArgs e)
        {
            await UploadImage(false);
        }

        private async Task UploadImage(bool isSocial)
        {
            if (string.IsNullOrEmpty(_imgurUrl))
            {
                var cookies = GetCookies();
                var imgurData = new ImgurAlbumData();
                var imgurImageData = new ImgurImageData();
                var errorMessage = string.Empty;

                ProgressLabel.Content = "Checking captcha";
                ProgressLabel.Visibility = Visibility.Visible;
                GeneralProgressBar.Visibility = Visibility.Visible;

                if (!checkCaptcha(cookies, ref imgurData, ref errorMessage))
                {
                    MessageBox.Show(errorMessage, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    if (imgurData.Success)
                    {
                        ProgressLabel.Content = "Generating image";
                        var file = new FileInfo(CreateImage(true));

                        if (File.Exists(file.FullName))
                        {
                            ProgressLabel.Content = "Uploading image";

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

                            await task;

                            if (!isSocial)
                            {
                                ImgurDataGrid.Visibility = Visibility.Visible;
                                ImgurUrlTxtBx.Text = $"http://imgur.com/{imgurImageData.Data.Hash}";
                                _imgurUrl = ImgurUrlTxtBx.Text;
                            }
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
                if (!isSocial)
                    ImgurDataGrid.Visibility = Visibility.Visible;
            }
        }

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

        public static long ToUnixTimestamp(DateTime target)
        {
            var date = new DateTime(1970, 1, 1, 0, 0, 0, target.Kind);
            var unixTimestamp = ToInt64((target - date).TotalSeconds);

            return unixTimestamp;
        }

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

        private void DismissImgurBtn_Click(object sender, RoutedEventArgs e)
        {
            ImgurDataGrid.Visibility = Visibility.Hidden;
        }

        private void CopySpecsButton_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(ClipboardContentTxtBlck.Text);
            MessageBox.Show("Specs copied as text to Clipboard!", "Copied", MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private async void TwitterShareButton_Click(object sender, RoutedEventArgs e)
        {
            await UploadImage(true);

            var tweetUrl =
                $"https://twitter.com/intent/tweet?hashtags=PCGaming,CPUCores&text={HttpUtility.UrlEncode("Check out my PC specs! via CPUCores")}&url={ImgurUrlTxtBx.Text}";
            Process.Start(tweetUrl);
        }
    }

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

    public class ImgurAlbumData
    {
        [JsonProperty("data")]
        public AlbumData Data { get; set; }

        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("status")]
        public int Status { get; set; }
    }

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