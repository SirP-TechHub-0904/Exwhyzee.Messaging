
using Exwhyzee.Messaging.Core.Paystack.Models.Verification;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Exwhyzee.Messaging.Core.Paystack
{
    public interface IVerification
    {
        Task<BVNVerificationResponseModel> ResolveBVN(string bvn);
    }
}

