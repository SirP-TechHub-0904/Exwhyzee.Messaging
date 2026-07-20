using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Exwhyzee.Messaging.Core.Models;
using Exwhyzee.Messaging.Core.Data.Services;
using Exwhyzee.Messaging.Core.Data;
using Microsoft.Extensions.DependencyInjection;

class Program
{
    static void Main()
    {
        try 
        {
            using (var db = new ApplicationDbContext())
            {
                var client = db.Clients.Include(x => x.User).FirstOrDefault();
                Console.WriteLine("Success: " + (client != null));
            }
        }
        catch(Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
    }
}