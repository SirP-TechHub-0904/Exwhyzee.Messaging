using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Exwhyzee.Messaging.Core.Models;
using Exwhyzee.Messaging.Core.Data;

class Program
{
    static void Main()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost;Database=dummy;")
            .Options;
            
        using (var db = new ApplicationDbContext(options))
        {
            var sql = db.Clients.Include(x => x.User).ToQueryString();
            Console.WriteLine("GENERATED SQL:");
            Console.WriteLine(sql);
        }
    }
}