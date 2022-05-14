using Exwhyzee.Messaging.Web.Data.IServices;
using Exwhyzee.Messaging.Web.Data.Services;
using Exwhyzee.Messaging.Web.Models;
using Microsoft.AspNet.Identity.Owin;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace Exwhyzee.Messaging.Web.Areas.ClientPanel.Controllers
{
    [Authorize(Roles = "Client")]
    public class AddressBookController : Controller
    {
        private ApplicationDbContext db = new ApplicationDbContext();

        private IAddressBookService _addressBookService = new AddressBookService();
        private ApplicationUserManager _userManager;

        public AddressBookController()
        {
        }

        public AddressBookController(AddressBookService addressBookService, ApplicationUserManager userManager)
        {
            _addressBookService = addressBookService;
            _userManager = userManager;
        }

        public ApplicationUserManager UserManager
        {
            get
            {
                return _userManager ?? HttpContext.GetOwinContext().GetUserManager<ApplicationUserManager>();
            }
            private set
            {
                _userManager = value;
            }
        }

        // GET: ClientPanel/AddressBook
        public async Task<ActionResult> Index()
        {
            var user = await UserManager.FindByNameAsync(User.Identity.Name);
            return View(await _addressBookService.GetAllGroups(user.Id));
        }

        // GET: ClientPanel/AddressBook/Details/5
        public async Task<ActionResult> Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Group group = await _addressBookService.GetGroup(id);
            if (group == null)
            {
                return HttpNotFound();
            }
            return View(group);
        }

        // GET: ClientPanel/AddressBook/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: ClientPanel/AddressBook/Create
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create([Bind(Include = "GroupId,Name,Description")] Group group)
        {
            if (ModelState.IsValid)
            {
                var user = await UserManager.FindByNameAsync(User.Identity.Name);
                group.UserId = user.Id;
                await _addressBookService.CreateGroup(group);
                return RedirectToAction("Index");
            }

            return View(group);
        }

        // GET: ClientPanel/AddressBook/AddContact
        public async Task<ActionResult> AddContact(int? id)
        {
            Group group = await _addressBookService.GetGroup(id);
            if (group == null)
            {
                return HttpNotFound();
            }
            return View();
        }

        // POST: ClientPanel/AddressBook/Create
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> AddContact(Contact contact, int id, string dateBirth)
        {
            if (dateBirth != "")
            {
                contact.DateOfBirth = DateTime.ParseExact(dateBirth.ToString(), "MM/dd/yyyy", null);
            }

            if (ModelState.IsValid)
            {
                contact.GroupId = id;
                await _addressBookService.NewContact(contact);
                return RedirectToAction("Details", new { id = id });
            }

            return View(contact);
        }


        /// <summary>
        /// 
        /// 
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        /// 
              // GET: ClientPanel/AddressBook/AddContact
        public async Task<ActionResult> AddManyContact(int? id)
        {
            Group group = await _addressBookService.GetGroup(id);
            if (group == null)
            {
                return HttpNotFound();
            }
            return View();
        }

        // POST: ClientPanel/AddressBook/Create
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> AddManyContact(int id, string contact, string Description, string Name)
        {
            contact = contact.Replace("\r\n", ",");
            IList<string> numbers = contact.Split(new string[] { ",", " " }, StringSplitOptions.RemoveEmptyEntries);
            var numbersplit = numbers.Distinct().ToList();
            if (ModelState.IsValid)
            {
                foreach (var newcontact in numbersplit)
                {


                    Contact i = new Contact();
                    i.GroupId = id;
                    i.Surname = Name;
                    i.PhoneNumber = newcontact;
                    i.Note = Description;
                    await _addressBookService.NewContact(i);
                }
                return RedirectToAction("Details", new { id = id });
            }

            return View(contact);
        }

        // GET: ClientPanel/AddressBook/Edit/5
        public async Task<ActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Group group = await _addressBookService.GetGroup(id);
            if (group == null)
            {
                return HttpNotFound();
            }
            return View(group);
        }

        // POST: ClientPanel/AddressBook/Edit/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(Group group)
        {
            if (ModelState.IsValid)
            {
                await _addressBookService.UpdateGroup(group);
                return RedirectToAction("Index");
            }
            return View(group);
        }

        // GET: ClientPanel/AddressBook/Edit/5
        public async Task<ActionResult> EditContact(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Contact contact = await _addressBookService.GetContact(id);
            if (contact == null)
            {
                return HttpNotFound();
            }
            var groups = db.Groups.OrderBy(x => x.Name).Where(x => x.UserId == contact.Group.UserId);
            ViewBag.GroupId = new SelectList(groups, "GroupId", "Name", contact.GroupId);
            return View(contact);
        }

        // POST: ClientPanel/AddressBook/Edit/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> EditContact(Contact contact, string dateBirth)
        {
            if (dateBirth != "")
            {
                contact.DateOfBirth = DateTime.ParseExact(dateBirth.ToString(), "MM/dd/yyyy", null);
            }
            if (ModelState.IsValid)
            {
                var groupId = contact.GroupId;
                await _addressBookService.UpdateContact(contact);
                return RedirectToAction("Details", new { id = groupId });
            }
            return View(contact);
        }

        // GET: ClientPanel/AddressBook/Delete/5
        public async Task<ActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Group group = await _addressBookService.GetGroup(id);
            if (group == null)
            {
                return HttpNotFound();
            }
            return View(group);
        }

        // POST: ClientPanel/AddressBook/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteConfirmed(int id)
        {
            Group group = await _addressBookService.GetGroup(id);

            await _addressBookService.DeleteGroup(group);
            return RedirectToAction("Index");
        }

        // GET: ClientPanel/AddressBook/Delete/5
        public async Task<ActionResult> DeleteContact(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Contact contact = await _addressBookService.GetContact(id);
            if (contact == null)
            {
                return HttpNotFound();
            }
            return View(contact);
        }

        // POST: ClientPanel/AddressBook/Delete/5
        [HttpPost, ActionName("DeleteContact")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteContactConfirmed(int id)
        {
            Contact contact = await _addressBookService.GetContact(id);
            int? groupId = contact.GroupId;
            await _addressBookService.DeleteContact(contact);
            return RedirectToAction("Details", new { id = groupId });
        }
        //delete all contact

        // GET: ClientPanel/AddressBook/Delete/5
        public async Task<ActionResult> DeleteAll(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Group group = await _addressBookService.GetGroup(id);
            if (group == null)
            {
                return HttpNotFound();
            }
            return View(group);
        }

        // POST: ClientPanel/AddressBook/Delete/5
        [HttpPost, ActionName("DeleteAll")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteAllConfirmed(int id)
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
            TempData["Success"] = "Deleted All";
            return RedirectToAction("Details", new { id = groupId });
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