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
            
            // Show the user row
            SqlCommand cmd = new SqlCommand("SELECT UserName, NormalizedUserName, Email, NormalizedEmail FROM AspNetUsers WHERE UserName = 'onwuka1'", conn);
            using (SqlDataReader reader = cmd.ExecuteReader())
            {
                if (reader.Read())
                {
                    Console.WriteLine("UserName: " + reader[0] + " | NormalizedUserName: " + (reader.IsDBNull(1) ? "NULL" : reader[1]));
                    Console.WriteLine("Email: " + reader[2] + " | NormalizedEmail: " + (reader.IsDBNull(3) ? "NULL" : reader[3]));
                }
                else 
                {
                    Console.WriteLine("User 'onwuka1' not found in database.");
                }
            }
        }
    }
}