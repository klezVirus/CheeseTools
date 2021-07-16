using CheeseSQL.Helpers;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;


namespace CheeseSQL.Commands
{
    public class getlogin : ICommand
    {
        public static string CommandName => "getlogin";

        public string Description()
        {
            return $"[*] {CommandName}\r\n" +
                   $"  Description: Retrieve SQL Logins Available for Impersonation";
        }

        public string Usage()
        {
            return $"{Description()}\r\n  " +
                $"Usage: {System.Reflection.Assembly.GetExecutingAssembly().GetName().Name} {CommandName} " +
                $"/db:DATABASE " +
                $"/server:SERVER " +
                $"[/impersonate:USER] " +
                $"[/sqlauth /user:SQLUSER " +
                $"/password:SQLPASSWORD]";
        }

        public void Execute(Dictionary<string, string> arguments)
        {
            string connectInfo = "";
            string database = "";
            string connectserver = "";
            string impersonate = "";

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
            if (arguments.ContainsKey("/impersonate"))
            {
                impersonate = arguments["/impersonate"];
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

            SqlCommand command;
            SqlDataReader reader;

            var queries = new List<string>();

            if (!String.IsNullOrEmpty(impersonate))
            {
                queries.Add($"EXECUTE AS LOGIN = '{impersonate}';");
            }
            queries.Add("SELECT SYSTEM_USER as 'Logged in as', CURRENT_USER as 'Mapped as';");

            foreach (string query in queries)
            {
                SQLExecutor.ExecuteQuery(connection, query);
            }

            /*
             Here, we try to identify not only impersonable users, but also impersonable user who are also owners of a trustworthy DB
             */
            try
            {
                string queryImpersonable = "SELECT distinct b.name FROM sys.server_permissions a INNER JOIN sys.server_principals b ON a.grantor_principal_id = b.principal_id WHERE a.permission_name = 'IMPERSONATE'";
                command = new SqlCommand(queryImpersonable, connection);

                List<string> users = new List<string>();
                using (reader = command.ExecuteReader())
                {

                    while (reader.Read() == true)
                    {
                        string tmp = reader.GetString(0);
                        Console.WriteLine("[+] This user can be impersonated: {0}", tmp);
                        users.Add(tmp);
                    }
                }
                if (users.Count > 0)
                {
                    foreach (string u in users)
                    {
                        String query = $"select name, suser_sname( owner_sid ) as owner, is_trustworthy_on from sys.databases where owner = '{u}'";
                        command = new SqlCommand(queryImpersonable, connection);

                        using (reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                if (reader.FieldCount < 3)
                                {
                                    continue;
                                }
                                bool isTrustworthy = reader[2] as bool? ?? false;
                                if (isTrustworthy)
                                {
                                    Console.WriteLine("[+] Trustworthy DB: {0}, Owner: {1}", reader[0], reader[1]);
                                }

                            }
                        }
                    }
                }
                else
                {
                    Console.WriteLine("[-] No user can be impersonated");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"[-] Error auditing impersonation: {e.Message}");
            }

            connection.Close();

        }
    }
}
