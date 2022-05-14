using Exwhyzee.Messaging.Web.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Exwhyzee.Messaging.Web.Data.IServices
{
    public interface IAddressBookService
    {
        Task<List<Group>> GetAllGroups(string userId);

        Task<List<Contact>> GetContactsInGroup(int? groupId);

        Task CreateGroup(Group item);

        Task NewContact(Contact item);

        Task DeleteContact(Contact item);

        Task DeleteGroup(Group item);

        Task<Group> GetGroup(int? groupId);

        Task<Contact> GetContact(int? contactId);

        Task UpdateGroup(Group item);

        Task UpdateContact(Contact item);
    }
}