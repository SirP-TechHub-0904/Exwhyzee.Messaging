using System;
using System.Collections.Generic;
using System.Text;

namespace Exwhyzee.Messaging.Web.PayStack
{
    public interface IChargeHelper
    {
        int AddCharge(int amountInKobo);
        decimal CalculatedCharge(int amountInKobo);
    }
}
