using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace Exwhyzee.Messaging.Web.ViewModels
{
    public class LoadVourcherViewModel
    {
        [Required]
        public string Pin { get; set; }

        [Required]
        public string BatchNumber { get; set; }
    }
}