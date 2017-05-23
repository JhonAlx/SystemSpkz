using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
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
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private const string Bits64 = "64-bit";
        private const string Bits32 = "32-bit";

        public ManagementObjectCollection GetManagementClassProperties(string id)
        {
            return new ManagementClass(id).GetInstances();
        }

        public MainWindow()
        {
            InitializeComponent();

            GetOsInfo();
            GetCpuInfo();
            GetMemoryInfo();
            GetMoBoInfo();
            GetVideoInfo();
            GetStorageInfo();
            GetOpticalDrivesInfo();
            GetAudioInfo();
            GetNetworkInfo();
        }

        private void GetNetworkInfo()
        {
            NetworkInfoLabel.Content = "";

            foreach (var mgmtObj in GetManagementClassProperties("Win32_NetworkAdapter"))
            {
                if(mgmtObj["NetConnectionStatus"] != null && mgmtObj["PhysicalAdapter"] != null)
                    if(mgmtObj["NetConnectionStatus"].ToString() == "2" && ToBoolean(mgmtObj["PhysicalAdapter"]))
                        NetworkInfoLabel.Content += $"{mgmtObj["Description"]}{Environment.NewLine}";
            }

            NetworkInfoLabel.Content = NetworkInfoLabel.Content.ToString()
                .TrimEnd()
                .Substring(0, NetworkInfoLabel.Content.ToString().Length - 2);
        }

        private void GetAudioInfo()
        {
            AudioInfoLabel.Content = "";

            foreach (var mgmtObj in GetManagementClassProperties("Win32_SoundDevice"))
            {
                AudioInfoLabel.Content += $"{mgmtObj["Caption"]}{Environment.NewLine}";
            }

            AudioInfoLabel.Content = AudioInfoLabel.Content.ToString()
                .TrimEnd()
                .Substring(0, AudioInfoLabel.Content.ToString().Length - 2);
        }

        private void GetOpticalDrivesInfo()
        {
            OpticalInfoLabel.Content = "";

            foreach (var mgmtObj in GetManagementClassProperties("Win32_CDROMDrive"))
            {
                OpticalInfoLabel.Content += $"{mgmtObj["Caption"]} / ";
            }

            OpticalInfoLabel.Content = OpticalInfoLabel.Content.ToString()
                .TrimEnd()
                .Substring(0, OpticalInfoLabel.Content.ToString().Length - 2);
        }

        private void GetStorageInfo()
        {
            StorageInfoLabel.Content = "";

            foreach (var mgmtObj in GetManagementClassProperties("Win32_DiskDrive"))
            {
                var storageAmount = Math.Ceiling((double)ToInt64(mgmtObj["Size"]) / 1073741824);
                StorageInfoLabel.Content += $"{mgmtObj["Caption"]} {storageAmount} GB{Environment.NewLine}";
            }

            StorageInfoLabel.Content = StorageInfoLabel.Content.ToString()
                .TrimEnd()
                .Substring(0, StorageInfoLabel.Content.ToString().Length - 2);
        }

        private void GetVideoInfo()
        {
            VideoInfoLabel.Content = "";

            foreach (var mgmtObj in GetManagementClassProperties("Win32_VideoController"))
            {
                var vramAmount = Math.Ceiling((double) ToInt64(mgmtObj["AdapterRAM"]) / 1073741824);
                VideoInfoLabel.Content += $"{mgmtObj["Caption"]} {vramAmount} GB{Environment.NewLine}";
            }

            VideoInfoLabel.Content = VideoInfoLabel.Content.ToString()
                .TrimEnd()
                .Substring(0, VideoInfoLabel.Content.ToString().Length - 2);
        }

        private void GetMoBoInfo()
        {
            var cpuSocket = string.Empty;

            foreach (var mgmtObj in GetManagementClassProperties("Win32_Processor"))
            {
                cpuSocket = mgmtObj["SocketDesignation"].ToString();
            }

            foreach (var mgmtObj in GetManagementClassProperties("Win32_BaseBoard"))
            {
                MoBoInfoLabel.Content = $"{mgmtObj["Manufacturer"]} {mgmtObj["Product"]} ({cpuSocket})";
            }
        }

        private void GetMemoryInfo()
        {
            RamInfoLabel.Content = "";

            foreach (var mgmtObj in GetManagementClassProperties("Win32_PhysicalMemory"))
            {
                RamInfoLabel.Content += $"{ToInt64(mgmtObj["Capacity"]) / 1073741824} GB {mgmtObj["Manufacturer"]} @ {mgmtObj["Speed"]} MHz{Environment.NewLine}";
            }

            RamInfoLabel.Content = RamInfoLabel.Content.ToString()
                .TrimEnd()
                .Substring(0, RamInfoLabel.Content.ToString().Length - 2);
        }

        private void GetCpuInfo()
        {
            foreach (var mgmtObj in GetManagementClassProperties("Win32_Processor"))
            {
                CpuInfoLabel.Content = $"{mgmtObj["Name"]}";
            }
        }

        private void GetOsInfo()
        {
            foreach (var mgmtObj in GetManagementClassProperties("Win32_OperatingSystem"))
            {
                var architecture = Environment.Is64BitOperatingSystem ? Bits64 : Bits32;
                OsInfoLabel.Content = $"{mgmtObj["Caption"]} {architecture}";
            }
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
                var openDialog = new SaveFileDialog()
                {
                    Filter = "Image files (*.png) | *.png;"
                };

                if (openDialog.ShowDialog() == true)
                {
                    path = openDialog.FileName;
                }
            }
            else
            {
                path = Path.Combine(Environment.CurrentDirectory, "temp.png");
            }

            if (!string.IsNullOrEmpty(path))
            {
                int height = (int) DataWrapper.ActualHeight;
                int width = (int) DataWrapper.ActualWidth;

                RenderTargetBitmap bmp = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
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

        private void UploadImageButton_Click(object sender, RoutedEventArgs e)
        {
            var cookies = GetCookies();
            var imgurData = new ImgurAlbumData();
            var imgurImageData = new ImgurImageData();
            var errorMessage = string.Empty;
            
            if (!checkCaptcha(cookies, ref imgurData, ref errorMessage))
            {
                MessageBox.Show(errorMessage);
            }
            else
            {
                if (imgurData.Success)
                {
                    var file = new FileInfo(CreateImage(true));

                    if (File.Exists(file.FullName))
                        using (var fs = new FileStream(file.FullName, FileMode.Open, FileAccess.Read))
                        {
                            var data = new byte[fs.Length];
                            fs.Read(data, 0, data.Length);

                            var postParameters = new Dictionary<string, object>
                            {
                                {"new_album_id", imgurData.Data.NewAlbumId},
                                {
                                    "file",
                                    new FormUpload.FileParameter(data, file.Name, MimeMapping.GetMimeMapping(file.FullName))
                                }
                            };

                            using (var s = FormUpload.MultipartFormDataPost("http://imgur.com/upload",
                                    "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/56.0.2924.87 Safari/537.36",
                                    postParameters, cookies)
                                .GetResponseStream())
                            {
                                using (var sr = new StreamReader(s, Encoding.UTF8))
                                {
                                    imgurImageData = JsonConvert.DeserializeObject<ImgurImageData>(sr.ReadToEnd());
                                }
                            }

                            MessageBox.Show($"http://imgur.com/{imgurImageData.Data.Hash}");
                        }
                    else
                        MessageBox.Show("File does not exist on the specified directory!");
                }
                else
                {
                    MessageBox.Show("Try again later!");
                }
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
            cookie2.Value = HttpUtility.UrlEncode($"{{\"sessionCount\":1,\"sessionTime\":{ToUnixTimestamp(DateTime.Now)}}}");
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
            StringBuilder sb = new StringBuilder();

            using (var sha = SHA256.Create())
            {
                var enc = Encoding.UTF8;
                byte[] result = sha.ComputeHash(
                    enc.GetBytes(
                        "20030107Google Inc.08MozillaNetscape5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537.36Win32GeckoMozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537.36estruetruenullundefinedundefinedundefinedundefinedundefined2017 - 05 - 23T05: 50:13.522Z"));

                foreach (var b in result)
                    sb.Append(b.ToString("x2"));
            }

            return sb.ToString();
        }

        private bool checkCaptcha(CookieContainer cookies, ref ImgurAlbumData imgurData, ref string errorMessage)
        {
            try
            {
                const string captchaUrl = "http://imgur.com/upload/checkcaptcha";
                var captchaRequest = WebRequest.Create(captchaUrl) as HttpWebRequest;
                
                if (captchaRequest != null)
                {
                    string captchaPostString = "total_uploads=1&create_album=true";
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
                                    imgurData = JsonConvert.DeserializeObject<ImgurAlbumData>(sr.ReadToEnd());
                                }
                        }
                }

                if (imgurData != null)
                {
                    return imgurData.Success;
                }
            }
            catch (Exception e)
            {
                errorMessage = e.Message + Environment.NewLine + e.StackTrace;
                return false;
            }
            
            return false;
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
