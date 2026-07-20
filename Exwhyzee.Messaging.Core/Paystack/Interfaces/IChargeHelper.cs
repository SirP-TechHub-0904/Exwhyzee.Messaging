using System;
using System.Collections.Generic;
using System.Text;

namespace Exwhyzee.Messaging.Core.Paystack
{
    public interface IChargeHelper
    {
        int AddCharge(int amountInKobo);
        decimal CalculatedCharge(int amountInKobo);
    }
}

