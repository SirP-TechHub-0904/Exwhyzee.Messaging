using System;
using System.Data.SqlClient;

class Program
{
    static void Main()
    {
        string connStr = "Data Source=147.93.128.160,1433;Initial Catalog=DB_9AFABF_xyzsmsdb;User Id=user_db;Password=Exwhyzee@123;TrustServerCertificate=True;";
        using (SqlConnection conn = new SqlConnection(connStr))
        {
            conn.Open();
            
            // Fix AspNetUsers
            SqlCommand cmdUsers = new SqlCommand("UPDATE AspNetUsers SET NormalizedUserName = UPPER(UserName), NormalizedEmail = UPPER(Email) WHERE NormalizedUserName IS NULL", conn);
            int usersUpdated = cmdUsers.ExecuteNonQuery();
            Console.WriteLine("Updated " + usersUpdated + " users.");

            // Fix AspNetRoles
            SqlCommand cmdRoles = new SqlCommand("UPDATE AspNetRoles SET NormalizedName = UPPER(Name) WHERE NormalizedName IS NULL", conn);
            int rolesUpdated = cmdRoles.ExecuteNonQuery();
            Console.WriteLine("Updated " + rolesUpdated + " roles.");
        }
    }
}