using Exwhyzee.Messaging.Core.Data.IServices;
using Exwhyzee.Messaging.Core.Data.Services;
using Exwhyzee.Messaging.Core.Models;
using Exwhyzee.Messaging.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Exwhyzee.Messaging.Web.Areas.ClientPanel.Controllers
{
    [Authorize(Roles = "Client")]
    [Area("ClientPanel")]
    public class AddressBookController : Controller
    {
        private readonly ApplicationDbContext db;
        private readonly IAddressBookService _addressBookService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IGeminiContactExtractorService _geminiExtractor;

        public AddressBookController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IAddressBookService addressBookService = null, 
            IGeminiContactExtractorService geminiExtractor = null)
        {
            db = context ?? new ApplicationDbContext();
            _addressBookService = addressBookService ?? new AddressBookService();
            _userManager = userManager;
            _geminiExtractor = geminiExtractor;
        }

        // GET: ClientPanel/AddressBook
        public async Task<ActionResult> Index()
        {
            var user = await _userManager.FindByNameAsync(User.Identity.Name);
            var client = await db.Clients.FirstOrDefaultAsync(c => c.UserId == user.Id);
            ViewBag.HasGeminiKey = !string.IsNullOrEmpty(client?.GeminiApiKey);
            return View(await _addressBookService.GetAllGroups(user.Id));
        }

        // GET: ClientPanel/AddressBook/Details/5
        public async Task<ActionResult> Details(int? id)
        {
            if (id == null)
            {
                return BadRequest();
            }
            Group group = await _addressBookService.GetGroup(id);
            if (group == null)
            {
                return NotFound();
            }
            var user = await _userManager.FindByNameAsync(User.Identity.Name);
            var client = await db.Clients.FirstOrDefaultAsync(c => c.UserId == user.Id);
            ViewBag.HasGeminiKey = !string.IsNullOrEmpty(client?.GeminiApiKey);
            return View(group);
        }

        // GET: ClientPanel/AddressBook/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: ClientPanel/AddressBook/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(Group group)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByNameAsync(User.Identity.Name);
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
                return NotFound();
            }
            return View();
        }

        // POST: ClientPanel/AddressBook/AddContact
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> AddContact(Contact contact, int id, string dateBirth)
        {
            if (!string.IsNullOrEmpty(dateBirth))
            {
                if (DateTime.TryParse(dateBirth, out DateTime dob))
                {
                    contact.DateOfBirth = dob;
                }
            }

            if (ModelState.IsValid)
            {
                contact.GroupId = id;
                contact.IsActive = true;
                await _addressBookService.NewContact(contact);
                return RedirectToAction("Details", new { id = id });
            }

            return View(contact);
        }

        // GET: ClientPanel/AddressBook/AddManyContact
        public async Task<ActionResult> AddManyContact(int? id)
        {
            Group group = await _addressBookService.GetGroup(id);
            if (group == null)
            {
                return NotFound();
            }
            var user = await _userManager.FindByNameAsync(User.Identity.Name);
            var client = await db.Clients.FirstOrDefaultAsync(c => c.UserId == user.Id);
            ViewBag.HasGeminiKey = !string.IsNullOrEmpty(client?.GeminiApiKey);
            ViewBag.Group = group;
            ViewBag.GroupId = id;
            return View();
        }

        // POST: ClientPanel/AddressBook/AddManyContact
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> AddManyContact(int id, string contact, string Description, string Name)
        {
            if (string.IsNullOrWhiteSpace(contact))
            {
                TempData["error"] = "Please enter contacts to add.";
                return RedirectToAction("Details", new { id = id });
            }

            contact = contact.Replace("\r\n", ",");
            var numbers = contact.Split(new string[] { ",", " ", ";", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            var numbersplit = numbers.Distinct().ToList();

            foreach (var newcontact in numbersplit)
            {
                var cleaned = System.Text.RegularExpressions.Regex.Replace(newcontact, @"[^\d+]", "");
                if (cleaned.Length >= 8)
                {
                    Contact i = new Contact
                    {
                        GroupId = id,
                        Surname = string.IsNullOrWhiteSpace(Name) ? "Contact" : Name,
                        PhoneNumber = cleaned,
                        Note = Description,
                        IsActive = true
                    };
                    await _addressBookService.NewContact(i);
                }
            }
            TempData["Success"] = $"{numbersplit.Count} contacts added successfully!";
            return RedirectToAction("Details", new { id = id });
        }

        // GET: ClientPanel/AddressBook/Edit/5
        public async Task<ActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return BadRequest();
            }
            Group group = await _addressBookService.GetGroup(id);
            if (group == null)
            {
                return NotFound();
            }
            return View(group);
        }

        // POST: ClientPanel/AddressBook/Edit/5
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

        // GET: ClientPanel/AddressBook/Delete/5
        public async Task<ActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return BadRequest();
            }
            Group group = await _addressBookService.GetGroup(id);
            if (group == null)
            {
                return NotFound();
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

        // POST: ClientPanel/AddressBook/DeleteAll
        [HttpPost, ActionName("DeleteAll")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteAllConfirmed(int id)
        {
            var group = await _addressBookService.GetGroup(id);
            if (group != null && group.Contacts != null)
            {
                foreach (var i in group.Contacts.ToList())
                {
                    try
                    {
                        Contact contact = await _addressBookService.GetContact(i.ContactId);
                        await _addressBookService.DeleteContact(contact);
                    }
                    catch { }
                }
            }
            TempData["Success"] = "All contacts deleted from group";
            return RedirectToAction("Details", new { id = id });
        }

        #region AI Vision, Document Parsing & Contact Management AJAX Endpoints

        private static string NormalizePhone(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return "";
            string digits = System.Text.RegularExpressions.Regex.Replace(phone, @"[^\d]", "");
            if (digits.StartsWith("234") && digits.Length == 13)
            {
                return "0" + digits.Substring(3); // Standardize to 080... format for duplicate matching
            }
            return digits;
        }

        [HttpPost]
        public async Task<IActionResult> ExtractContactsAi(IFormFile file, string pasteImageBase64, string rawText, int? groupId = null)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var client = await db.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
                string geminiKey = client?.GeminiApiKey;

                List<ExtractedContactDto> results = new List<ExtractedContactDto>();

                // 1. Pasted Screenshot (Base64)
                if (!string.IsNullOrWhiteSpace(pasteImageBase64))
                {
                    if (string.IsNullOrWhiteSpace(geminiKey))
                    {
                        return Json(new { success = false, keyMissing = true, message = "Please configure your Google Gemini API Key in 'API Settings & Keys' to enable Screenshot AI extraction." });
                    }

                    string cleanBase64 = pasteImageBase64;
                    string mime = "image/png";
                    if (cleanBase64.Contains(","))
                    {
                        var parts = cleanBase64.Split(',');
                        cleanBase64 = parts[1];
                        if (parts[0].Contains("jpeg") || parts[0].Contains("jpg")) mime = "image/jpeg";
                        else if (parts[0].Contains("webp")) mime = "image/webp";
                    }

                    byte[] imageBytes = Convert.FromBase64String(cleanBase64);
                    results = await _geminiExtractor.ExtractFromImageAsync(imageBytes, mime, geminiKey);
                }
                // 2. Uploaded File (Image, docx, xlsx, txt, csv)
                else if (file != null && file.Length > 0)
                {
                    using var ms = new MemoryStream();
                    await file.CopyToAsync(ms);
                    byte[] fileBytes = ms.ToArray();
                    string ext = Path.GetExtension(file.FileName).ToLower();

                    // If image, check Gemini key
                    if ((ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".webp") && string.IsNullOrWhiteSpace(geminiKey))
                    {
                        return Json(new { success = false, keyMissing = true, message = "Please configure your Google Gemini API Key in 'API Settings & Keys' to enable Image/Screenshot AI extraction." });
                    }

                    results = await _geminiExtractor.ExtractFromFileAsync(fileBytes, file.FileName, geminiKey);
                }
                // 3. Raw Text Notes / Chat Logs
                else if (!string.IsNullOrWhiteSpace(rawText))
                {
                    results = await _geminiExtractor.ExtractFromTextAsync(rawText, geminiKey);
                }
                else
                {
                    return Json(new { success = false, message = "Please upload a file, paste an image screenshot, or provide text." });
                }

                // Check for duplicates in specific target group
                var existingNormalized = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (groupId.HasValue && groupId.Value > 0)
                {
                    var existingGroupContacts = await db.Contacts.Where(c => c.GroupId == groupId.Value).ToListAsync();
                    foreach (var c in existingGroupContacts)
                    {
                        string norm = NormalizePhone(c.PhoneNumber);
                        if (!string.IsNullOrEmpty(norm)) existingNormalized.Add(norm);
                    }
                }

                int newCount = 0;
                int duplicateCount = 0;
                var enrichedList = new List<object>();

                foreach (var item in results)
                {
                    string norm = NormalizePhone(item.Phone);
                    bool isDuplicate = !string.IsNullOrEmpty(norm) && existingNormalized.Contains(norm);

                    if (isDuplicate) duplicateCount++;
                    else newCount++;

                    enrichedList.Add(new
                    {
                        name = item.Name,
                        phone = item.Phone,
                        note = item.Note,
                        isDuplicate = isDuplicate
                    });
                }

                return Json(new
                {
                    success = true,
                    totalCount = results.Count,
                    newCount = newCount,
                    duplicateCount = duplicateCount,
                    contacts = enrichedList
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SaveExtractedContacts([FromBody] SaveContactsRequest request)
        {
            try
            {
                if (request == null || request.Contacts == null || !request.Contacts.Any())
                {
                    return Json(new { success = false, message = "No contacts provided to save." });
                }

                var user = await _userManager.FindByNameAsync(User.Identity.Name);
                int targetGroupId = request.GroupId ?? 0;

                // Create new group if requested or needed
                if (targetGroupId == 0)
                {
                    string groupName = string.IsNullOrWhiteSpace(request.NewGroupName) ? $"AI Import ({DateTime.Now:MMM dd, yyyy HH:mm})" : request.NewGroupName.Trim();
                    var newGroup = new Group
                    {
                        Name = groupName,
                        Description = "Contacts imported via AI Extractor",
                        UserId = user.Id,
                        DateCreated = DateTime.UtcNow
                    };
                    db.Groups.Add(newGroup);
                    await db.SaveChangesAsync();
                    targetGroupId = newGroup.GroupId;
                }

                // Existing numbers in this specific group to prevent duplicate insertion
                var existingGroupContacts = await db.Contacts.Where(c => c.GroupId == targetGroupId).ToListAsync();
                var existingSet = new HashSet<string>(
                    existingGroupContacts.Select(c => NormalizePhone(c.PhoneNumber)),
                    StringComparer.OrdinalIgnoreCase
                );

                // Batch insert only unique new contacts into this group
                var contactsToAdd = new List<Contact>();
                int skippedDuplicates = 0;

                foreach (var item in request.Contacts)
                {
                    if (!string.IsNullOrWhiteSpace(item.Phone))
                    {
                        string norm = NormalizePhone(item.Phone);
                        if (!string.IsNullOrEmpty(norm))
                        {
                            if (existingSet.Contains(norm))
                            {
                                skippedDuplicates++;
                                continue; // Skip duplicate inside this group
                            }
                            existingSet.Add(norm);

                            string cleanPhone = System.Text.RegularExpressions.Regex.Replace(item.Phone, @"[^\d+]", "");
                            if (cleanPhone.Length >= 8)
                            {
                                contactsToAdd.Add(new Contact
                                {
                                    GroupId = targetGroupId,
                                    Surname = string.IsNullOrWhiteSpace(item.Name) ? "Contact" : item.Name.Trim(),
                                    PhoneNumber = cleanPhone,
                                    Note = string.IsNullOrWhiteSpace(item.Note) ? "Imported" : item.Note.Trim(),
                                    IsActive = true,
                                    DateAddded = DateTime.UtcNow
                                });
                            }
                        }
                    }
                }

                if (contactsToAdd.Any())
                {
                    db.Contacts.AddRange(contactsToAdd);
                    await db.SaveChangesAsync();
                }

                return Json(new
                {
                    success = true,
                    groupId = targetGroupId,
                    count = contactsToAdd.Count,
                    skipped = skippedDuplicates,
                    message = skippedDuplicates > 0
                        ? $"{contactsToAdd.Count} new contacts added ({skippedDuplicates} existing duplicates were automatically skipped)."
                        : $"{contactsToAdd.Count} contacts added successfully!"
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ToggleContactActive(int contactId)
        {
            try
            {
                var contact = await db.Contacts.FirstOrDefaultAsync(c => c.ContactId == contactId);
                if (contact == null) return Json(new { success = false, message = "Contact not found" });

                contact.IsActive = !contact.IsActive;
                db.Entry(contact).State = EntityState.Modified;
                await db.SaveChangesAsync();

                return Json(new { success = true, contactId = contactId, isActive = contact.IsActive });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> BatchToggleGroupContacts(int groupId, bool setActive)
        {
            try
            {
                var contacts = await db.Contacts.Where(c => c.GroupId == groupId).ToListAsync();
                foreach (var c in contacts)
                {
                    c.IsActive = setActive;
                }
                await db.SaveChangesAsync();

                return Json(new { success = true, count = contacts.Count, isActive = setActive });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteContactAjax(int contactId)
        {
            try
            {
                var contact = await db.Contacts.FirstOrDefaultAsync(c => c.ContactId == contactId);
                if (contact != null)
                {
                    db.Contacts.Remove(contact);
                    await db.SaveChangesAsync();
                    return Json(new { success = true, contactId = contactId });
                }
                return Json(new { success = false, message = "Contact not found" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        #endregion
    }

    public class SaveContactsRequest
    {
        public int? GroupId { get; set; }
        public string NewGroupName { get; set; }
        public List<ExtractedContactDto> Contacts { get; set; }
    }
}