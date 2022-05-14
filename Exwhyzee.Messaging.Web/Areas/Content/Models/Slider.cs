using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace Exwhyzee.Messaging.Web.Areas.Content.Models
{
    public class Slider
    {
        public int SliderId { get; set; }
        [Display(Name = "Image Name")]
        public string ImageName { get; set; }
        [Display(Name = "ImageUrl")]
        public string ImageUrl { get; set; }
        [Display(Name = "Status")]
        public Status Status { get; set; }
        [Display(Name = "Caption")]
        public string Caption { get; set; }
    }
}