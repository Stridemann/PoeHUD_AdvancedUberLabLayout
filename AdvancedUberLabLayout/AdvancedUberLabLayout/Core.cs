using PoeHUD.Controllers;
using PoeHUD.Hud.UI;
using PoeHUD.Models;
using PoeHUD.Models.Enums;
using PoeHUD.Plugins;
using PoeHUD.Poe;
using PoeHUD.Poe.Components;
using PoeHUD.Poe.Elements;
using PoeHUD.Poe.FilesInMemory;
using PoeHUD.Poe.RemoteMemoryObjects;
using SharpDX;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Bitmap = System.Drawing.Bitmap;

namespace AdvancedUberLabLayout
{
    //http://www.poelab.com/wp-content/uploads/2017/09/2017-09-25_normal.jpg
    public class Core : BaseSettingsPlugin<Settings>
    {
        #region ClassVariables
        public Core()
        {
            PluginName = "Advanced Uber Lab";
        }
        private const string CachedImages = "CachedImages";
        private const string ImageLink = "http://www.poelab.com/wp-content/uploads/";
        private const int ImageWidth = 841;
        private const int ImageHeight = 270;

        private string ImagesDirectory => Path.Combine(PluginDirectory, CachedImages);

        private DateTime UTCTime;
        private DateTime LabResetTime;

        private string ImagePathToDraw;

        private ImageCheckState ImageState = ImageCheckState.Undefined;

        private string[] LabTypes = new string[]
        {
            "normal",
            "cruel",
            "merciless",
            "uber"
        };
        #endregion

        private void OnAreaChange(AreaController area)
        {
            if (Settings.AutoLabDetection.Value)
            {
                switch (area.CurrentArea.RealLevel)
                {
                    case 33:
                        Settings.LabType.Value = LabTypes[0];
                        break;
                    case 55:
                        Settings.LabType.Value = LabTypes[1];
                        break;
                    case 68:
                        Settings.LabType.Value = LabTypes[2];
                        break;
                    case 75:
                        Settings.LabType.Value = LabTypes[3];
                        break;
                }
            }
        }

        public override void Initialise()
        {
            UpdateTime();

            #pragma warning disable 4014
            LoadImage();
            #pragma warning restore 4014

            Settings.LabType.SetListValues(LabTypes.ToList());

            #pragma warning disable 4014
            Settings.LabType.OnValueSelected += delegate { LoadImage(); };
            #pragma warning restore 4014

            GameController.Area.OnAreaChange += OnAreaChange;
        }

        public override void Render()
        {
            if (Settings.ToggleDraw.PressedOnce())
                Settings.ShowImage = !Settings.ShowImage;

            if (!Settings.ShowImage) return;

            if(Settings.Reload.PressedOnce())
                #pragma warning disable 4014
                LoadImage();
                #pragma warning restore 4014

            var drawRect = new RectangleF(Settings.X, Settings.Y, ImageWidth * Settings.Size / 100f, ImageHeight * Settings.Size / 100f);

            UpdateTime();

            if(ImageState == ImageCheckState.ReadyToDraw)
            {
                var color = Color.White;
                color.A = (byte)(Settings.Transparency);
                Graphics.DrawPluginImage(ImagePathToDraw, drawRect, color);

                var toNextReset = LabResetTime - UTCTime;
                Graphics.DrawText("Reset in: " + toNextReset.ToString(@"hh\:mm\:ss"), 15, drawRect.TopLeft + new Vector2(10, 5));

                var name = Path.GetFileNameWithoutExtension(ImagePathToDraw);
                Graphics.DrawText(name, 15, drawRect.TopRight + new Vector2(-10, 5), SharpDX.Direct3D9.FontDrawFlags.Right);

            }
        }

        private void UpdateTime()
        {
            UTCTime = DateTime.Now.ToUniversalTime();
            LabResetTime = UTCTime.Date.AddDays(1);

            if(Settings.CurrentImageDateDay != UTCTime.Day)
            {
                #pragma warning disable 4014
                LoadImage();
                #pragma warning restore 4014
            }
        }

        private async Task LoadImage()
        {
            Settings.CurrentImageDateDay = UTCTime.Day;

            ImageState = ImageCheckState.Checking;
            await LoadData(DateTime.Now);

            if(ImageState == ImageCheckState.NotFound404)
            {
                LogMessage("New lab layout image is not created yet. Loading old image: " + (DateTime.Now.AddDays(-1).ToString("dd MMMM")), 10);

                await LoadData(DateTime.Now.AddDays(-1));

                if (ImageState == ImageCheckState.NotFound404)
                {
                    LogMessage("Failed loading OLD lab layout image: " + (DateTime.Now.AddDays(-1).ToString("dd MMMM")), 10);
                    ImageState = ImageCheckState.FailedToLoad;
                }
            }
        }

        private async Task LoadData(DateTime data)
        {
            var time = data;
            var month = time.Month.ToString("00");
            var day = time.Day.ToString("00");

            var tempFileName = $"{time.Year}-{month}-{day}_{Settings.LabType.Value}.jpg";

            var FilePath = Path.Combine(ImagesDirectory, tempFileName);

            if(File.Exists(FilePath))
            {
                LogMessage("Loading lab layout image from cache", 3);
                ImagePathToDraw = FilePath;
                ImageState = ImageCheckState.ReadyToDraw;
                return;
            }

            LogMessage("Loading new lab layout image from site", 3);

            var ImageUrl = $"http://www.poelab.com/wp-content/uploads/{time.Year}/{month}/" + tempFileName;

            ImageState = ImageCheckState.Downloading;

            WebClient webClient = new WebClient();

            byte[] imageBytes = new byte[0];
            try
            {
                imageBytes = await webClient.DownloadDataTaskAsync(ImageUrl);
            }
            catch// (Exception ex)
            {
                ImageState = ImageCheckState.NotFound404;
                //Not found
                return;
            }
            
            Bitmap downloadedImage;
            try
            {
                using (var ms = new MemoryStream(imageBytes))
                {
                    downloadedImage = new Bitmap(ms);
                }

                if (!Directory.Exists(ImagesDirectory))
                    Directory.CreateDirectory(ImagesDirectory);

                downloadedImage = new Bitmap(downloadedImage);//Fix for exception (bitmap save problem https://stackoverflow.com/questions/5813633/a-generic-error-occurs-at-gdi-at-bitmap-save-after-using-savefiledialog )

                downloadedImage = downloadedImage.Clone(new System.Drawing.Rectangle(302, 111, ImageWidth, ImageHeight), downloadedImage.PixelFormat);
                
                downloadedImage.Save(FilePath, System.Drawing.Imaging.ImageFormat.Jpeg);

                ImageState = ImageCheckState.ReadyToDraw;
                ImagePathToDraw = FilePath;

                downloadedImage.Dispose();
            }
            catch (Exception ex)
            {
                LogError("AdvancedUberLabLayout Plugin: Error while cropping or saving image: " + ex.Message, 10);
                ImageState = ImageCheckState.FailedToLoad;
            }
            finally
            {
                //downloadedImage.Dispose();
            }
        }

        private enum ImageCheckState
        {
            Undefined,
            Checking,
            NotFound404,
            Downloading,
            ReadyToDraw,
            FailedToLoad
        }

        private void DeleteOldImages()
        {
            var imgFiles = Directory.GetFiles(Path.Combine(PluginDirectory, CachedImages));
            foreach (var dir in imgFiles)
                File.Delete(dir);
        }
    }
}
