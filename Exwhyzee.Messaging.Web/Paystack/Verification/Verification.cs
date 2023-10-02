using Newtonsoft.Json;
using Exwhyzee.Messaging.Web.PayStack.Models.Verification;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Exwhyzee.Messaging.Web.PayStack.Verification
{
    public class Verification : IVerification
    {
        string _secretKey;
        public Verification(string secretKey)
        {
            this._secretKey = secretKey;
        }

        public async Task<BVNVerificationResponseModel> ResolveBVN(string bvn)
        {
            var client = HttpConnection.CreateClient(this._secretKey);
            var response = await client.GetAsync($"bank/resolve_bvn/{bvn}");

            var json = await response.Content.ReadAsStringAsync();

            return JsonConvert.DeserializeObject<BVNVerificationResponseModel>(json);
        }
    }
}
