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
            SqlCommand cmd = new SqlCommand("SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Client'", conn);
            using (SqlDataReader reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    Console.WriteLine(reader.GetString(0));
                }
            }
        }
    }
}