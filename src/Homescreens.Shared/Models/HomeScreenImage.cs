using System;

namespace Homescreens.Shared.Models
{
    public class HomeScreenImage
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public DateTime UploadedOn { get; set; }

        /// <summary>
        /// Phone, browser, watch, etc
        /// </summary>
        /// <remarks>Normally this would be an enum, but not sure how DDB would handle</remarks>

        public string Type { get; set; }
        public string UserName { get; set; }

        public string FileName { get; set; }

        /// <summary>
        /// Whether or not the image has been processed and is
        /// published
        /// </summary>
        public bool IsPublished { get; set; }
    }
}
