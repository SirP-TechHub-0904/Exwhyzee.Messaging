using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Exwhyzee.Messaging.Core.Models
{
    public class ModalInfo
    {
        public int Id { get; set; }

        public string Title { get; set; }

        public string Modal { get; set; }

        public string ContentHtml { get; set; }

        public string ImageUrl { get; set; }

        [Display(Name = "Display Image Banner")]
        public bool UseImage { get; set; } = true;

        [Display(Name = "Display Text / Write-Up")]
        public bool UseText { get; set; } = true;

        public bool IsActive { get; set; } = true;

        [Display(Name = "Repeat Frequency (Days)")]
        public int ShowFrequencyDays { get; set; } = 7;

        public DateTime DateCreated { get; set; } = DateTime.UtcNow;
    }
}
