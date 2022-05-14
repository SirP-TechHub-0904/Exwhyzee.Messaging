using Exwhyzee.Messaging.Web.Data.IServices;
using Exwhyzee.Messaging.Web.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace Exwhyzee.Messaging.Web.Data.Services
{
    public class AddressBookService : IAddressBookService
    {
        private ApplicationDbContext db = new ApplicationDbContext();

        public async Task CreateGroup(Group item)
        {
            db.Groups.Add(item);
            await db.SaveChangesAsync();
        }

        public async Task DeleteContact(Contact item)
        {
            db.Contacts.Remove(item);
            await db.SaveChangesAsync();
        }

        public async Task DeleteGroup(Group item)
        {
            db.Groups.Remove(item);
            await db.SaveChangesAsync();
        }

        public async Task<List<Contact>> GetContactsInGroup(int? groupId)
        {
            var addressBook = db.Contacts.OrderBy(c => c.Surname).Where(x => x.GroupId == groupId);
            return await addressBook.ToListAsync();
        }

        public async Task<List<Group>> GetAllGroups(string userId)
        {
            var groups = db.Groups.Include(x=>x.Contacts).OrderBy(o => o.Name).Where(x => x.UserId == userId);
            return await groups.ToListAsync();
        }

        public async Task<Contact> GetContact(int? contactId)
        {
            var contact = await db.Contacts.Include(g => g.Group).FirstOrDefaultAsync(x => x.ContactId == contactId);
            return contact;
        }

        public async Task<Group> GetGroup(int? groupId)
        {
            var group = await db.Groups.Include(c => c.Contacts).FirstOrDefaultAsync(x => x.GroupId == groupId);
            return group;
        }

        public async Task NewContact(Contact item)
        {
            db.Contacts.Add(item);
            await db.SaveChangesAsync();
        }

        public async Task UpdateContact(Contact item)
        {
            db.Entry(item).State = EntityState.Modified;
            await db.SaveChangesAsync();
        }

        public async Task UpdateGroup(Group item)
        {
            db.Entry(item).State = EntityState.Modified;

            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!GroupExists(item.GroupId))
                {
                    throw new Exception("No Found");
                }
                else
                {
                    throw;
                }
            }
         
        }


        private bool GroupExists(int id)
        {
            return db.Groups.Count(e => e.GroupId == id) > 0;
        }
    }
}