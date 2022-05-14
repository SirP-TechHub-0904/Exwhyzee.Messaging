using Hangfire;
using Exwhyzee.Messaging.Web.Data.IServices;
using Exwhyzee.Messaging.Web.Dtos;
using Exwhyzee.Messaging.Web.Models;
using Exwhyzee.Messaging.Web.Services;
using Exwhyzee.Messaging.Web.ViewModels;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;
using System.Text.RegularExpressions;

namespace Exwhyzee.Messaging.Web.Data.Services
{
    public class ClientService : IClientService
    {
        private ApplicationDbContext db = new ApplicationDbContext();

        public async Task AddMessageToHistory(Message item)
        {
            db.Messages.Add(item);
            await db.SaveChangesAsync();
        }

        public async Task AddUnit(int id, Transaction item)
        {
            var user = await db.Clients.FindAsync(id);
            user.Units = user.Units + item.Units;
            db.Transactions.Add(item);
            await db.SaveChangesAsync();
        }

        public async Task<Voucher> CheckVoucher(string code, string batchNumber)
        {
            var check = await db.Vouchers.Include(c => c.BactchVoucher).FirstOrDefaultAsync(x => x.Code == code && x.BactchVoucher.BatchNumber == batchNumber && x.Status == VoucherStatus.UnUsed);

            if (check != null)
            {
                return check;
            }
            return null;
        }

        public async Task<List<Message>> GetAllMessageHistory()
        {
            var messages = db.Messages.OrderByDescending(x => x.MessageId).Where(x => x.Status != MessageStatus.Draft);
            return await messages.ToListAsync();
        }

        public async Task<decimal> GetAmount(decimal units)
        {
            var pricePerUnit = await db.AdminSettings.FirstOrDefaultAsync();
            decimal amount = units * pricePerUnit.PricePerUnit;
            return amount;
        }

        public async Task<Client> GetClientDetails(int clientId)
        {
            var clientDetails = await db.Clients.Include(x => x.User).FirstOrDefaultAsync(x => x.ClientId == clientId);

            return clientDetails;
        }

        public async Task<Client> GetClientDetailsByUserId(string userId)
        {
            var clientDetails = await db.Clients.Include(x=>x.User).FirstOrDefaultAsync(x => x.UserId == userId);

            return clientDetails;
        }

        public async Task<List<Message>> GetClientDraftMessages(string userId)
        {
            var messages = db.Messages.OrderByDescending(x => x.MessageId).Where(x => x.Status == MessageStatus.Draft && x.UserId == userId);
            return await messages.ToListAsync();
        }

        public async Task<List<Message>> GetClientMessageHistory(string userId)
        {
            var messages = db.Messages.OrderByDescending(x => x.MessageId).Where(x => x.Status != MessageStatus.Draft && x.UserId == userId);
            return await messages.ToListAsync();
        }

        public async Task<Message> GetMessage(int? id)
        {
            Message message = await db.Messages.FindAsync(id);

            return message;
        }

        public async Task<List<Transaction>> GetUserTransactions(int id)
        {
            var transactions = db.Transactions.OrderByDescending(x => x.TransactionId).Where(x => x.ClientId == id);
            return await transactions.ToListAsync();
        }

        public async Task LoadVoucher(Voucher item)
        {
            db.Entry(item).State = EntityState.Modified;

            //TOP UP UNITS
            var client = await db.Clients.FirstOrDefaultAsync(x => x.UserId == item.UserId);
            client.Units = client.Units + item.Units;
            db.Entry(client).State = EntityState.Modified;
            //Add to Transactions
            Transaction transaction = new Transaction();
            transaction.Amount = 0;
            transaction.AmountPaid = 0;
            transaction.ClientId = client.ClientId;
            transaction.DateApproved = DateTime.UtcNow;
            transaction.DateCreated = DateTime.UtcNow;
            transaction.Status = TransactionStatus.Approved;
            transaction.TransactionType = TransactionType.ByVoucher;
            transaction.Units = item.Units;
            transaction.UserId = item.BactchVoucher.UserId;
            db.Transactions.Add(transaction);

            await db.SaveChangesAsync();
        }

        public async Task<string> SendSms(string senderId, string message, string recipients)
        {
            //get API to use
            var getApi = await db.ApiSettings.FirstOrDefaultAsync(x => x.IsDefault == true);
            string apiSending = getApi.Sending.Replace("@@sender@@", HttpUtility.UrlEncode(senderId)).Replace("@@recipient@@", HttpUtility.UrlEncode(recipients)).Replace("@@message@@", HttpUtility.UrlEncode(message));

            HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(apiSending);
            httpWebRequest.Method = "GET";
            httpWebRequest.ContentType = "application/json";
            httpWebRequest.Timeout = 25000;

            //getting the respounce from the request
            HttpWebResponse httpWebResponse = (HttpWebResponse)await httpWebRequest.GetResponseAsync();
            Stream responseStream = httpWebResponse.GetResponseStream();
            StreamReader streamReader = new StreamReader(responseStream);
            string response = await streamReader.ReadToEndAsync();

            return response;
        }

        public async Task<SendMessageResponseDto> ComposeSms(ComposeSmsDto model, string userId)
        {
            try
            {
                DateTime scheduleDate = DateTime.Now;
                if (model.ScheduleDate != null)
                {
                    scheduleDate = DateTime.ParseExact(model.ScheduleDate.ToString(), "dd/MM/yyyy HH:mm", null);
                }

                //Get API Setting to use
                var getApi = await db.ApiSettings.FirstOrDefaultAsync(x => x.IsDefault == true);

                if (getApi == null)
                {
                    return new SendMessageResponseDto
                    {
                        Message = "Sending Message failed",
                        Description = "No API setting is set as Default.",
                        Success = false,
                        ResponseCode = "401"
                    };
                }

                //get Client Details
                var client = await db.Clients.FirstOrDefaultAsync(x => x.UserId == userId);
                if (client == null)
                {
                    return new SendMessageResponseDto
                    {
                        Message = "Sending Message failed",
                        Description = "Client Details not found.",
                        Success = false,
                        ResponseCode = "404"
                    };
                }

                //Get Recipients from Group ID and add to other Recipients
                if (model.GroupId != null)
                {
                    string contacts;
                    string combined = "";

                    foreach (var item in model.GroupId)
                    {
                        var itemContacts = db.Contacts.Where(x => x.GroupId == item).Select(x => x.PhoneNumber);
                        contacts = string.Join(",", itemContacts.ToList());
                        combined = combined + contacts;
                    }

                    model.Recipients = model.Recipients + combined;
                }


                //Check if recipients is empty
                if (string.IsNullOrEmpty(model.Recipients))
                {

                    return new SendMessageResponseDto
                    {
                        Message = "Message Sending failed.",
                        Description = "No recipient was added or Address book Group selected. If you want to save as DRAFT, add any number to Recipient to save as Draft.",
                        Success = false,
                        ResponseCode = "400"
                    };
                }

                //Get page count
                int pageCount = SmsServices.CountPage(model.Content);

                //Remove Duplicates
                List<string> numbers = new List<string>(SmsServices.RemoveDuplicates(model.Recipients));

                //Format Numbers with International dail codes
                List<string> fNumbers = new List<string>(SmsServices.FormatNumbers(numbers.ToList()));

                //units needed per page
                decimal units = SmsServices.UnitsPerPage(fNumbers.ToList());

                //total units needed
                decimal totalUnitsNeeded = pageCount * units;

                //Check if Client's Unit is sufficient
                if (totalUnitsNeeded > client.Units)
                {
                    return new SendMessageResponseDto
                    {
                        Message = "Sending Message failed",
                        Description = "You have insufficient unit balance. Your current balance is " + client.Units + "units, while total units required is " + totalUnitsNeeded + ".",
                        Success = false,
                        ResponseCode = "401"
                    };
                }


                //Save message to History
                Message message = new Message();
                message.DeliveredDate = DateTime.UtcNow;
                message.MessageContent = model.Content;
                message.Recipients = string.Join(",", fNumbers.ToList());
                message.Response = "Pending";
                message.SenderId = model.SenderId.ToString();
                if (model.SendOption == "SendLater")
                {
                    message.Status = MessageStatus.Scheduled;
                }
                else if (model.SendOption == "SaveDraft")
                {
                    message.Status = MessageStatus.Draft;
                }
                else
                {
                    message.Status = MessageStatus.Pending;
                }

                message.UnitsUsed = 0;
                message.UserId = client.UserId;

                await AddMessageToHistory(message);

                if (model.SendOption == "SendNow")
                {
                    var response = await SendSmsById(message.MessageId, totalUnitsNeeded);
                    var sentMessage = await GetMessage(message.MessageId);
                    if (response.ToUpper().Contains("OK") || response.ToUpper().Contains("1701"))
                    {
                        //Update Client
                        client.Units = client.Units - totalUnitsNeeded;
                        await UpdateClient(client);

                        sentMessage.UnitsUsed = totalUnitsNeeded;
                        sentMessage.Status = MessageStatus.Sent;
                        sentMessage.Response = response;
                        await UpdateMessageStatus(sentMessage);

                        return new SendMessageResponseDto
                        {
                            Message = "Message has been sent successfully.",
                            Description = "Total Units used is " + totalUnitsNeeded + ".",
                            Success = true,
                            ResponseCode = response
                        };


                    }
                    else
                    {
                        return new SendMessageResponseDto
                        {
                            Message = "Sending Message Failed.",
                            Description = response + ". Sending Message Failed. Please try again or Contact the Administrator.",
                            Success = false,
                            ResponseCode = response
                        };

                    }
                }
                else if (model.SendOption == "SendLater")
                {
                    DateTime currentTime = DateTime.UtcNow.AddHours(1);
                    TimeSpan elapsedTime = scheduleDate.Subtract(currentTime);

                    BackgroundJob.Schedule(() => SendLater(message.MessageId, client.ClientId, totalUnitsNeeded), TimeSpan.FromMinutes(elapsedTime.TotalMinutes));

                    return new SendMessageResponseDto
                    {
                        Message = "Message has been Scheduled.",
                        Description = "Message has been scheduled to send in " + elapsedTime.Minutes + "mins",
                        Success = true,
                        ResponseCode = "Ok"
                    };

                }
                else if (model.SendOption == "SaveDraft")
                {
                    return new SendMessageResponseDto
                    {
                        Message = "Message has been Saved.",
                        Description = "Message has been Saved as Draft.",
                        Success = true,
                        ResponseCode = "Ok"
                    };

                }
            }
            catch(Exception er)
            {
                return new SendMessageResponseDto
                {
                    Message = "Error.",
                    Description = "Something wrong happened.",
                    Success = true,
                    ResponseCode = er.Message
                };
            }

            return new SendMessageResponseDto
            {
                Message = "Error.",
                Description = "Something wrong happened.",
                Success = true,
                ResponseCode = "500"
            };

        }

        public async Task SendLater(int messageId, int clientId, decimal totalUnitsNeeded)
        {
            var client = await GetClientDetails(clientId);
            var sentMessage = await GetMessage(messageId);

            //Check if Client's Unit is sufficient
            if (totalUnitsNeeded > client.Units)
            {
                throw new Exception("You don't have enough Units for this transaction.");
            }
            else
            {
                var response = await SendSmsById(messageId, totalUnitsNeeded);
                if (response.ToUpper().Contains("OK") || response.ToUpper().Contains("1701"))
                {
                    //Update Client
                    client.Units = client.Units - totalUnitsNeeded;
                    
                    await UpdateClient(client);

                    sentMessage.UnitsUsed = totalUnitsNeeded;
                    sentMessage.Status = MessageStatus.Sent;
                    sentMessage.Response = response;
                    await UpdateMessageStatus(sentMessage);
                }
                else
                {
                    throw new Exception("Message Sending failed.");
                }
            }
        }

        public async Task<string> SendSmsById(int messageHistoryId, decimal units)
        {
            var messageHistory = await db.Messages.FindAsync(messageHistoryId);
            var getApi = await db.ApiSettings.FirstOrDefaultAsync(x => x.IsDefault == true);
            //check our balance and theirs
            var getApiBal = await db.ApiSettings.FirstOrDefaultAsync(x => x.IsDefault == true);
            string apiSendingbal = getApiBal.CheckBalance;

            HttpWebRequest httpWebRequestBal = (HttpWebRequest)WebRequest.Create(apiSendingbal);
            httpWebRequestBal.Method = "GET";
            httpWebRequestBal.ContentType = "application/json";
            httpWebRequestBal.Timeout = 25000;

            //getting the respounce from the request
            HttpWebResponse httpWebResponseBal = (HttpWebResponse)await httpWebRequestBal.GetResponseAsync();
            Stream responseStreamBal = httpWebResponseBal.GetResponseStream();
            StreamReader streamReaderBal = new StreamReader(responseStreamBal);
            string responsebal = await streamReaderBal.ReadToEndAsync();
            ////response = response.Remove(0, 11);
            //// response = response.Substring(0, 5);
            ///string inputStr =  "($23.01)";      
            responsebal = Regex.Match(responsebal, @"\d+.+\d").Value;
            responsebal = responsebal.Substring(0, responsebal.IndexOf(','));
            decimal AdminBal = Convert.ToDecimal(responsebal);
            if(units > AdminBal)
            {
                string response1 = " error 88009-8899-333";
            
            return response1;
        }
            //get cost of message

            //end balance
            //
            string response = "";
            string chunknumbersSend = "";
            string SenderIdEncode = System.Web.HttpUtility.UrlEncode(messageHistory.SenderId);
            List<string> smsnumbers = new List<string>(SmsServices.RemoveDuplicates(messageHistory.Recipients));
            if (smsnumbers.Count() > 200)
            {


                


                var chunknumbers = splitList(smsnumbers, 200);
                
                foreach (var n in chunknumbers)
                {
                    chunknumbersSend  = string.Join(",", n.ToList());

                    string apiSending = getApi.Sending.Replace("@@sender@@", SenderIdEncode).Replace("@@recipient@@", HttpUtility.UrlEncode(chunknumbersSend)).Replace("@@message@@", HttpUtility.UrlEncode(messageHistory.MessageContent));

                    HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(apiSending);
                    httpWebRequest.Method = "GET";
                    httpWebRequest.ContentType = "application/json";

                    //getting the respounce from the request
                    HttpWebResponse httpWebResponse = (HttpWebResponse)await httpWebRequest.GetResponseAsync();
                    Stream responseStream = httpWebResponse.GetResponseStream();
                    StreamReader streamReader = new StreamReader(responseStream);
                    response = await streamReader.ReadToEndAsync();
                    //response = "ok";

                    MessageChunk chunk = new MessageChunk();
                    chunk.MessageId = messageHistory.MessageId;
                    chunk.Numbers = chunknumbersSend;
                    chunk.Response = response;
                    db.MessageChunks.Add(chunk);
                    await db.SaveChangesAsync();

                }
                if (response.Contains("Insufficient funds"))
                {
                    response = "error 1001110_10";
                }
                return response;
            }
            else
            {
                try
                {
                    
                    string apiSending = getApi.Sending.Replace("@@sender@@", SenderIdEncode).Replace("@@recipient@@", HttpUtility.UrlEncode(messageHistory.Recipients)).Replace("@@message@@", HttpUtility.UrlEncode(messageHistory.MessageContent));
                    
                    HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(apiSending);
                    httpWebRequest.Method = "GET";
                    httpWebRequest.ContentType = "application/json";

                    //getting the respounce from the request
                    HttpWebResponse httpWebResponse = (HttpWebResponse)await httpWebRequest.GetResponseAsync();
                    Stream responseStream = httpWebResponse.GetResponseStream();
                    StreamReader streamReader = new StreamReader(responseStream);
                    response = await streamReader.ReadToEndAsync();
                }catch(Exception c)
                {

                }
                //response = "ok";
                if (response.Contains("Insufficient funds"))
                {
                    response = "error 1001110_10";
                }
               
                return response;
            }

            
        }


      
        //chunk

        public static IEnumerable<List<T>> splitList<T>(List<T> numbers, int nSize = 200)
        {
            for (int i = 0; i < numbers.Count; i += nSize)
            {
                yield return numbers.GetRange(i, Math.Min(nSize, numbers.Count - i));
            }
        }

        public async Task UpdateClient(Client item)
        {
            try
            {
                db.Entry(item).State = EntityState.Modified;
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }
      
        public async Task UpdateMessageStatus(Message item)
        {
            db.Entry(item).State = EntityState.Modified;
            await db.SaveChangesAsync();
        }
    }

    public static class JosClientMain
    {
        public static async Task<List<JosSpcMessage>> ListMessageData()
        {
            try
            {
                using (StreamReader sr = new StreamReader(HttpContext.Current.Server.MapPath("~/Json/jsonone.json")))
                {
                    string json = sr.ReadToEnd();
                    List<JosSpcMessage> messages = await Task.Factory.StartNew(() => JsonConvert.DeserializeObject<List<JosSpcMessage>>(json));
                    var rept = messages.Select(x => x.recipients).ToList();

                    var data = string.Join(",", rept.ToList());
                    return messages;
                }
            }catch(Exception c)
            {

            }
            return null;
        }
    }
}