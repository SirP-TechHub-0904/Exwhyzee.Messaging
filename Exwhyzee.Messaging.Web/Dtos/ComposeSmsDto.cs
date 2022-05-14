using Exwhyzee.Messaging.Web.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace Exwhyzee.Messaging.Web.Dtos
{
    public class ComposeSmsDto
    {
        [Required]
        [MaxLength(11, ErrorMessage = "Sender Id can not be more than 11 characters.")]
        [RegularExpression("^[a-zA-Z ]*$", ErrorMessage = "You can use only Alphabets. Numbers and Non-alphanumeric characters are not allowed.")]
        public string SenderId { get; set; }

        [DataType(DataType.MultilineText)]
        public string Recipients { get; set; }

        [Required]
        [BlockBlackListedWords]
        [DataType(DataType.MultilineText)]
        public string Content { get; set; }

        [Required]
        public string SendOption { get; set; }

        //[DataType(DataType.Date)]
        public string ScheduleDate { get; set; }

        public int[] GroupId { get; set; }
    }
}