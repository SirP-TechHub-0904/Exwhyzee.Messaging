using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Exwhyzee.Messaging.Web.PayStack.Models
{
    public class TransactionListViewModel
    {
        public bool status { get; set; }
        public string message { get; set; }
        public List<Data> data { get; set; }
    }


}
