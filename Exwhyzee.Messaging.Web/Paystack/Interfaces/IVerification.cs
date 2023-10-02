
using Exwhyzee.Messaging.Web.PayStack.Models.Verification;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Exwhyzee.Messaging.Web.PayStack
{
    public interface IVerification
    {
        Task<BVNVerificationResponseModel> ResolveBVN(string bvn);
    }
}
