using CheeseSQL.Helpers;
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

            var procedures = new Dictionary<string, string>();

            if (!String.IsNullOrEmpty(impersonate))
            {
                procedures.Add($"Attempting impersonation as {impersonate}..", $"EXECUTE AS LOGIN = '{impersonate}';");
            }

            procedures.Add("Enabling RPC..", $"EXEC sp_serveroption '{target}', 'rpc', 'true'; EXEC sp_serveroption '{target}', 'rpc out', 'true';");

            foreach (string step in procedures.Keys)
            {
                Console.WriteLine("[*] {0}", step);
                SQLExecutor.ExecuteProcedure(connection, procedures[step]);
            }

            connection.Close();
        }
    }
}
