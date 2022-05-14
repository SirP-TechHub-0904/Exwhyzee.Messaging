using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Description;
using Exwhyzee.Messaging.Web.Data.IServices;
using Exwhyzee.Messaging.Web.Data.Services;
using Exwhyzee.Messaging.Web.Dtos;
using Exwhyzee.Messaging.Web.Models;
using Exwhyzee.Messaging.Web.Models.Dto;
using Microsoft.AspNet.Identity.Owin;

namespace Exwhyzee.Messaging.Web.Controllers
{
    public class GroupsController : ApiController
    {

            private ApplicationDbContext db = new ApplicationDbContext();
        private ApplicationUserManager _userManager;
        private IAddressBookService _addressBookService = new AddressBookService();
           

            public GroupsController()
            {
            }

            public GroupsController(AddressBookService addressBookService, ApplicationUserManager userManager)
            {
                _addressBookService = addressBookService;
                _userManager = userManager;
            }

      
        public ApplicationUserManager UserManager
        {
            get
            {
                return _userManager ?? Request.GetOwinContext().GetUserManager<ApplicationUserManager>();
            }
            private set
            {
                _userManager = value;
            }
        }


        // GET: ClientPanel/AddressBook
        [Route("AllGroups")]
        public async Task<HttpResponseMessage> AllGroups(string username)
            {
                var user = await UserManager.FindByNameAsync(username);
            var data = db.Groups.Include(x=>x.Contacts).OrderBy(o => o.Name).Where(x => x.UserId == user.Id);

            var output = data.Select(x => new GroupDto
            {
                GroupId = x.GroupId,
                DateCreated = x.DateCreated,
                SendBirthDayMessages = x.SendBirthDayMessages,
                Description = x.Description,
                Name = x.Name,
                UserId = x.UserId,
                Count = x.Contacts.Count()


            });

            return Request.CreateResponse(HttpStatusCode.OK, await output.ToListAsync());
            }

        [Route("AllContactByGroupId")]
        public async Task<HttpResponseMessage> AllContactByGroupId(int id)
        {
            var data = db.Contacts.OrderBy(o => o.Surname).Where(x => x.GroupId == id);
            if (data == null)
            {
                return Request.CreateResponse(HttpStatusCode.OK, "not found");
            }
            
            return Request.CreateResponse(HttpStatusCode.OK, await data.ToListAsync());
        }
        // GET: ClientPanel/AddressBook/Details/5
        [Route("GroupById")]
        public async Task<HttpResponseMessage> GroupById(int? id)
            {
                if (id == null)
                {
                return Request.CreateResponse(HttpStatusCode.OK, "Bad Request");
                }
                Group x = await _addressBookService.GetGroup(id);
                if (x == null)
                {
                return Request.CreateResponse(HttpStatusCode.OK, "HttpNotFound");
                }

            var output = new GroupDto
            {
                GroupId = x.GroupId,
                DateCreated = x.DateCreated,
                SendBirthDayMessages = x.SendBirthDayMessages,
                Description = x.Description,
                Name = x.Name,
                UserId = x.UserId,
                Count = x.Contacts.Count()


            };
            return Request.CreateResponse(HttpStatusCode.OK, output);
        }

           
            // POST: ClientPanel/AddressBook/Create
            // To protect from overposting attacks, please enable the specific properties you want to bind to, for
            // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
            [HttpPost]
        [Route("PostGroup")]
        public async Task<HttpResponseMessage> PostGroup(NewGroupModelDto group)
            {
                if (ModelState.IsValid)
                {
                    var user = await UserManager.FindByNameAsync(group.Username);
                Group data = new Group();
                data.UserId = user.Id;
                data.DateCreated = DateTime.UtcNow.AddHours(1);
                data.Description = group.Description;
                data.Message = group.Message;
                data.SendBirthDayMessages = group.SendBirthDayMessages;
                data.SenderId = group.SenderId;
                data.Name = group.Name;

                    await _addressBookService.CreateGroup(data);
                return Request.CreateResponse(HttpStatusCode.OK, "success");
            }

            return Request.CreateResponse(HttpStatusCode.OK, "failed");
        }

            // GET: ClientPanel/AddressBook/AddContact
           

            // POST: ClientPanel/AddressBook/Create
            // To protect from overposting attacks, please enable the specific properties you want to bind to, for
            // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
            [HttpPost]
        [Route("AddContact")]
        public async Task<HttpResponseMessage> AddContact(NewContactDto data)
            {
                
            Group x = await _addressBookService.GetGroup(data.GroupId);
            if (x == null)
            {
                return Request.CreateResponse(HttpStatusCode.OK, "Group not found");
            }
           
            if (ModelState.IsValid)
                {
                Contact ncontact = new Contact();
                    ncontact.GroupId = x.GroupId;
                ncontact.Note = data.Note;
                ncontact.Othernames = data.Othernames;
                ncontact.Surname = data.Surname;
                ncontact.DateAddded = DateTime.UtcNow.AddHours(1);
                ncontact.DateOfBirth = null;
                ncontact.PhoneNumber = data.PhoneNumber;
                
                    await _addressBookService.NewContact(ncontact);
                return Request.CreateResponse(HttpStatusCode.OK, "success");
            }

            return Request.CreateResponse(HttpStatusCode.OK, "failed");
        }



            // POST: ClientPanel/AddressBook/Create
            // To protect from overposting attacks, please enable the specific properties you want to bind to, for
            // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
            [HttpPost]
        [Route("AddManyContact")]
        public async Task<HttpResponseMessage> AddManyContact(NewContactDto data)
            {
                data.PhoneNumber = data.PhoneNumber.Replace("\r\n", ",");
                IList<string> numbers = data.PhoneNumber.Split(new string[] { ",", " " }, StringSplitOptions.RemoveEmptyEntries);
                var numbersplit = numbers.Distinct().ToList();

           
            Group x = await _addressBookService.GetGroup(data.GroupId);
            if (x == null)
            {
                return Request.CreateResponse(HttpStatusCode.OK, "Group not found");
            }
            if (ModelState.IsValid)
                {
                    foreach (var newcontact in numbersplit)
                    {


                        Contact i = new Contact();
                        i.GroupId = x.GroupId;
                        i.Surname = data.Surname;
                        i.PhoneNumber = newcontact;
                        i.Note = data.Note;
                    i.DateAddded = data.DateAddded;
                        await _addressBookService.NewContact(i);
                    }
                return Request.CreateResponse(HttpStatusCode.OK, "success");
            }

            return Request.CreateResponse(HttpStatusCode.OK, "failed");
        }


            // POST: ClientPanel/AddressBook/Edit/5
            // To protect from overposting attacks, please enable the specific properties you want to bind to, for
            // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
            [HttpPost]
        [Route("UpdateGroup")]
        public async Task<HttpResponseMessage> UpdateGroup(Group group)
            {
                if (ModelState.IsValid)
                {
                    await _addressBookService.UpdateGroup(group);
                return Request.CreateResponse(HttpStatusCode.OK, "success");
            }
            return Request.CreateResponse(HttpStatusCode.OK, "failed");
        }

          [HttpPost]
        [Route("EditContact")]
        public async Task<HttpResponseMessage> EditContact(Contact contact)
            {
                
                if (ModelState.IsValid)
                {
                    var groupId = contact.GroupId;
                    await _addressBookService.UpdateContact(contact);
                return Request.CreateResponse(HttpStatusCode.OK, "success");
            }
            return Request.CreateResponse(HttpStatusCode.OK, "failed");
        }

            // GET: ClientPanel/AddressBook/Delete/5
         

            // POST: ClientPanel/AddressBook/Delete/5
            [HttpPost]
        [Route("DeleteGroup")]
        public async Task<HttpResponseMessage> DeleteGroup(int id)
            {
                Group group = await _addressBookService.GetGroup(id);

                await _addressBookService.DeleteGroup(group);
            return Request.CreateResponse(HttpStatusCode.OK, "success");
        }

            // GET: ClientPanel/AddressBook/Delete/5
           
            // POST: ClientPanel/AddressBook/Delete/5
            [HttpPost]
        [Route("DeleteContact")]
        public async Task<HttpResponseMessage> DeleteContact(int id)
            {
                Contact contact = await _addressBookService.GetContact(id);
                int? groupId = contact.GroupId;
                await _addressBookService.DeleteContact(contact);
            return Request.CreateResponse(HttpStatusCode.OK, "success");
        }
            //delete all contact

           
            // POST: ClientPanel/AddressBook/Delete/5
            [HttpPost]
        [Route("DeleteAllContacts")]
        public async Task<HttpResponseMessage> DeleteAllContacts(int id)
            {
                var group = await _addressBookService.GetGroup(id);
                int? groupId = group.GroupId;
                foreach (var i in group.Contacts.ToList())
                {
                    try
                    {
                        Contact contact = await _addressBookService.GetContact(i.ContactId);
                        await _addressBookService.DeleteContact(contact);
                    }
                    catch (Exception c)
                    {

                    }
                }
            return Request.CreateResponse(HttpStatusCode.OK, "success");
        }

            protected override void Dispose(bool disposing)
            {
                if (disposing && _userManager != null)
                {
                    _userManager.Dispose();
                    _userManager = null;
                }

                base.Dispose(disposing);
            }


        }
    }