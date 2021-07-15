using System;
using System.Collections.Generic;
using System.Data.SqlClient;


namespace CheeseSQL.Commands
{
    public class rpc : ICommand
    {
        public static string CommandName => "rpc";

        public string Description()
        {
            return $"[*] {CommandName}\r\n" +
                   $"  Description: Configure Linked SQL Server to Allow RPC connections";
        }

        public string Usage()
        {
            return $"{Description()}\r\n  " +
                $"Usage: {System.Reflection.Assembly.GetExecutingAssembly().GetName().Name} {CommandName} " +
                $"/db:DATABASE " +
                $"/server:SERVER " +
                $"/target:TARGET " +
                $"[/impersonate:(USER|LOGIN):USER] " +
                $"[/sqlauth /user:SQLUSER /password:SQLPASSWORD]";
        }

        public void Execute(Dictionary<string, string> arguments)
        {

            string user = "";
            string password = "";
            string connectInfo = "";
            string database = "";
            string connectserver = "";
            string target = "";
            string impersonate = "";
            string impersonation_type = "LOGIN";

            bool sqlauth = false;

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
            if (arguments.ContainsKey("/target"))
            {
                target = arguments["/target"];
            }
            if (arguments.ContainsKey("/impersonate"))
            {
                impersonate = arguments["/impersonate"];

                if (impersonate.Contains(":"))
                {
                    impersonation_type = impersonate.Split(':')[0].Trim().ToUpperInvariant();
                    if (!(impersonation_type == "USER" || impersonation_type == "LOGIN"))
                    {
                        impersonation_type = "LOGIN";
                    }
                    impersonate = impersonate.Split(':')[1].Trim();
                }
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
            if (String.IsNullOrEmpty(target))
            {
                Console.WriteLine("\r\n[X] You must supply a target linked SQL server!\r\n");
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

            SqlCommand command;
            SqlDataReader reader;

            if (!String.IsNullOrEmpty(impersonate))
            {
                string execAs = $"EXECUTE AS {impersonation_type} = '{impersonate}';";
                // Console.WriteLine(execAs);
                command = new SqlCommand(execAs, connection);
                reader = command.ExecuteReader();
                reader.Read();
                Console.WriteLine("[*] Attempting impersonation as {0}..", impersonate);
                reader.Close();
            }

            string enableRPC = $"EXEC sp_serveroption '{target}', 'rpc', 'true'; EXEC sp_serveroption '{target}', 'rpc out', 'true';";
            command = new SqlCommand(enableRPC, connection);
            reader = command.ExecuteReader();
            reader.Read();
            Console.WriteLine("[*] Enabling RPC..");
            reader.Close();

            connection.Close();
        }
    }
}
