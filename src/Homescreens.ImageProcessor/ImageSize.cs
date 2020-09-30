using Amazon.Lambda.Core;

namespace Homescreens.ImageProcessor
{
    public class ImageSize
    {
        /// <summary>
        /// The device type of the image (phone, tablet, watch, browser)
        /// </summary>
        public string ImageType { get; set; }

        public string Key { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        public ImageSize(string imageType, string key, int width, int height)
        {
            ImageType = imageType;
            Key = key;
            Width = width;
            Height = height;
        }
    }
}
