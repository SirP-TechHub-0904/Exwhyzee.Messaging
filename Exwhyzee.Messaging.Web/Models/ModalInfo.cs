using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Exwhyzee.Messaging.Web.Models
{
    public class ModalInfo
    {
        public int Id { get; set; }

        [AllowHtml]
        public string Modal { get; set; }
    }
}