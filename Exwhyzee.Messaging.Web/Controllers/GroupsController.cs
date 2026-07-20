using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

using Exwhyzee.Messaging.Core.Data.IServices;
using Exwhyzee.Messaging.Core.Data.Services;
using Exwhyzee.Messaging.Core.Dtos;
using Exwhyzee.Messaging.Core.Models;
using Exwhyzee.Messaging.Core.Models.Dto;


namespace Exwhyzee.Messaging.Web.Controllers
{
    public class GroupsController : ControllerBase
    {

            private ApplicationDbContext db = new ApplicationDbContext();
        private Microsoft.AspNetCore.Identity.UserManager<ApplicationUser> _userManager;
        private IAddressBookService _addressBookService = new AddressBookService();
           

            

            public GroupsController(AddressBookService addressBookService, Microsoft.AspNetCore.Identity.UserManager<ApplicationUser> userManager)
            {
                _addressBookService = addressBookService;
                _userManager = userManager;
            }

      
        public Microsoft.AspNetCore.Identity.UserManager<ApplicationUser> UserManager
        {
            get
            {
                return _userManager;
            }
            private set
            {
                _userManager = value;
            }
        }


        // GET: ClientPanel/AddressBook
        [HttpGet]
        [Route("AllGroups")]
        public async Task<IActionResult> AllGroups(string username)
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

            return Ok(await output.ToListAsync());
            }

        [HttpGet]
        [Route("AllContactByGroupId")]
        public async Task<IActionResult> AllContactByGroupId(int id)
        {
            var data = db.Contacts.OrderBy(o => o.Surname).Where(x => x.GroupId == id);
            if (data == null)
            {
                return Ok("not found");
            }
            
            return Ok(await data.ToListAsync());
        }
        // GET: ClientPanel/AddressBook/Details/5
        [HttpGet]
        [Route("GroupById")]
        public async Task<IActionResult> GroupById(int? id)
            {
                if (id == null)
                {
                return Ok("Bad Request");
                }
                Group x = await _addressBookService.GetGroup(id);
                if (x == null)
                {
                return Ok("HttpNotFound");
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
            return Ok(output);
        }

           
            // POST: ClientPanel/AddressBook/Create
            // To protect from overposting attacks, please enable the specific properties you want to bind to, for
            // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
            [HttpPost]
        [Route("PostGroup")]
        public async Task<IActionResult> PostGroup(NewGroupModelDto group)
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
                return Ok("success");
            }

            return Ok("failed");
        }

            // GET: ClientPanel/AddressBook/AddContact
           

            // POST: ClientPanel/AddressBook/Create
            // To protect from overposting attacks, please enable the specific properties you want to bind to, for
            // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
            [HttpPost]
        [Route("AddContact")]
        public async Task<IActionResult> AddContact(NewContactDto data)
            {
                
            Group x = await _addressBookService.GetGroup(data.GroupId);
            if (x == null)
            {
                return Ok("Group not found");
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
                return Ok("success");
            }

            return Ok("failed");
        }



            // POST: ClientPanel/AddressBook/Create
            // To protect from overposting attacks, please enable the specific properties you want to bind to, for
            // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
            [HttpPost]
        [Route("AddManyContact")]
        public async Task<IActionResult> AddManyContact(NewContactDto data)
            {
                data.PhoneNumber = data.PhoneNumber.Replace("\r\n", ",");
                IList<string> numbers = data.PhoneNumber.Split(new string[] { ",", " " }, StringSplitOptions.RemoveEmptyEntries);
                var numbersplit = numbers.Distinct().ToList();

           
            Group x = await _addressBookService.GetGroup(data.GroupId);
            if (x == null)
            {
                return Ok("Group not found");
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
                return Ok("success");
            }

            return Ok("failed");
        }


            // POST: ClientPanel/AddressBook/Edit/5
            // To protect from overposting attacks, please enable the specific properties you want to bind to, for
            // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
            [HttpPost]
        [Route("UpdateGroup")]
        public async Task<IActionResult> UpdateGroup(Group group)
            {
                if (ModelState.IsValid)
                {
                    await _addressBookService.UpdateGroup(group);
                return Ok("success");
            }
            return Ok("failed");
        }

          [HttpPost]
        [Route("EditContact")]
        public async Task<IActionResult> EditContact(Contact contact)
            {
                
                if (ModelState.IsValid)
                {
                    var groupId = contact.GroupId;
                    await _addressBookService.UpdateContact(contact);
                return Ok("success");
            }
            return Ok("failed");
        }

            // GET: ClientPanel/AddressBook/Delete/5
         

            // POST: ClientPanel/AddressBook/Delete/5
            [HttpPost]
        [Route("DeleteGroup")]
        public async Task<IActionResult> DeleteGroup(int id)
            {
                Group group = await _addressBookService.GetGroup(id);

                await _addressBookService.DeleteGroup(group);
            return Ok("success");
        }

            // GET: ClientPanel/AddressBook/Delete/5
           
            // POST: ClientPanel/AddressBook/Delete/5
            [HttpPost]
        [Route("DeleteContact")]
        public async Task<IActionResult> DeleteContact(int id)
            {
                Contact contact = await _addressBookService.GetContact(id);
                int? groupId = contact.GroupId;
                await _addressBookService.DeleteContact(contact);
            return Ok("success");
        }
            //delete all contact

           
            // POST: ClientPanel/AddressBook/Delete/5
            [HttpPost]
        [Route("DeleteAllContacts")]
        public async Task<IActionResult> DeleteAllContacts(int id)
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
            return Ok("success");
        }

            


        }
    }