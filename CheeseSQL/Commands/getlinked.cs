using CheeseSQL.Helpers;
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

            SqlConnection connection;
            SQLExecutor.ConnectionInfo(arguments, connectserver, database, sqlauth, out connectInfo);
            if (String.IsNullOrEmpty(connectInfo))
            {
                return;
            }
            if (!SQLExecutor.Authenticate(connectInfo, out connection))
            {
                return;
            }

            /*
             I'm not porting this to SQLExecutor, again it's an output formattin issue
             */
            if (!verbose)
            {
                string procedure = "EXECUTE sp_linkedservers;";
                SqlCommand command = new SqlCommand(procedure, connection);

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Console.WriteLine("[*] Linked SQL server: " + reader[0]);
                    }
                }
            }
            else
            {
                string query = "SELECT name, is_linked, is_remote_login_enabled, is_data_access_enabled, is_rpc_out_enabled FROM sys.servers;";
                SqlCommand command = new SqlCommand(query, connection);

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
