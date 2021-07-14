using System;
using System.Collections.Generic;
using System.Data.SqlClient;


namespace CheeseSQL.Commands
{
    public class getlinked : ICommand
    {
        public static string CommandName => "getlinked";

        public string Description()
        {
            return $"[*] {CommandName}\r\n" +
    $"  Description: Retrieve Linked Servers";
        }

        public string Usage()
        {
            return $"{Description()}\r\n  " + 
                $"Usage: {System.Reflection.Assembly.GetExecutingAssembly().GetName().Name} {CommandName} /db:DATABASE /server:SERVER [/verbose] [/sqlauth /user:SQLUSER /password:SQLPASSWORD]";
        }

        public void Execute(Dictionary<string, string> arguments)
        {
            string user = "";
            string password = "";
            string connectInfo = "";
            string database = "";
            string connectserver = "";

            bool sqlauth = false;
            bool verbose = false;

            if (arguments.ContainsKey("/sqlauth"))
            {
                sqlauth = true;
            }
            if (arguments.ContainsKey("/db"))
            {
                database = arguments["/db"];
            }
            if (arguments.ContainsKey("/server"))
            {
                connectserver = arguments["/server"];
            }
            
            if (arguments.ContainsKey("/verbose"))
            {
                verbose = true;
            }

            if (String.IsNullOrEmpty(database))
            {
                Console.WriteLine("\r\n[X] You must supply a database!\r\n");
                return;
            }
            if (String.IsNullOrEmpty(connectserver))
            {
                Console.WriteLine("\r\n[X] You must supply an authentication server!\r\n");
                return;
            }

            if (sqlauth)
            {
                if (arguments.ContainsKey("/user"))
                {
                    user = arguments["/user"];
                }
                if (arguments.ContainsKey("/password"))
                {
                    password = arguments["/password"];
                }
                if (String.IsNullOrEmpty(user))
                {
                    Console.WriteLine("\r\n[X] You must supply the SQL account user!\r\n");
                    return;
                }
                if (String.IsNullOrEmpty(password))
                {
                    Console.WriteLine("\r\n[X] You must supply the SQL account password!\r\n");
                    return;
                }
                connectInfo = "Data Source= " + connectserver + "; Initial Catalog= " + database + "; User ID=" + user + "; Password=" + password;
            }
            else
            {
                connectInfo = "Server = " + connectserver + "; Database = " + database + "; Integrated Security = True;";
            }

            SqlConnection connection = new SqlConnection(connectInfo);

            try
            {
                connection.Open();
                Console.WriteLine($"[+] Authentication to the '{database}' Database on '{connectserver}' Successful!");
            }
            catch
            {
                Console.WriteLine($"[-] Authentication to the '{database}' Database on '{connectserver}' Failed.");
                return;
            }

            if (!verbose)
            {
                string execCmd = "EXECUTE sp_linkedservers;";
                SqlCommand command = new SqlCommand(execCmd, connection);

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Console.WriteLine("[*] Linked SQL server: " + reader[0]);
                    }
                }
            }
            else {
                string execCmd = "SELECT name, is_linked, is_remote_login_enabled, is_data_access_enabled, is_rpc_out_enabled FROM sys.servers;";
                SqlCommand command = new SqlCommand(execCmd, connection);

                using (SqlDataReader reader = command.ExecuteReader())
                {

                    while (reader.Read())
                    {
                        Console.Write($@"
[*] SQL server:   {reader.GetString(0)}
    Linked:       {reader.GetBoolean(1).ToString()}
    Remote Login: {reader.GetBoolean(2).ToString()}
    Data Access:  {reader.GetBoolean(3).ToString()}
    RPC out:      {reader.GetBoolean(4).ToString()}
    ----------------------------------------- 
");
                    }
                }

            }
            connection.Close();
        }
    }
}
