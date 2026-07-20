using System;
using System.Data.SqlClient;

class Program
{
    static void Main()
    {
        string connStr = "Data Source=147.93.128.160,1433;Initial Catalog=DB_9AFABF_xyzsmsdb;User Id=user_db;Password=Exwhyzee@123;TrustServerCertificate=True;";
        try
        {
            using (SqlConnection conn = new SqlConnection(connStr))
            {
                conn.Open();
                Console.WriteLine("SUCCESS");
            }
        }
        catch(Exception ex)
        {
            Console.WriteLine("ERROR: " + ex.Message);
        }
    }
}