using CheeseSQL.Helpers;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;

namespace CheeseSQL.Commands
{
    public class getserverinfo : ICommand
    {
        public static string CommandName => "getserverinfo";
        public string Description()
        {
            return $"[*] {CommandName}\r\n" +
                   $"  Description: Retrieve Information about the state of 'xp_cmdshell', 'ole automation' and 'clr'";
        }

        public string Usage()
        {
            return $"{Description()}\r\n  " +
                $"Usage: {System.Reflection.Assembly.GetExecutingAssembly().GetName().Name} {CommandName} " +
                $"/db:DATABASE " +
                $"/server:SERVER " +
                $"[/intermediate:INTERMEDIATE] " +
                $"[/target:TARGET] " +
                $"[/impersonate:USER] " +
                $"[/impersonate-intermediate:USER] " +
                $"[/impersonate-linked:USER] " +
                $"[/sqlauth /user:SQLUSER /password:SQLPASSWORD]";
        }

        public void Execute(Dictionary<string, string> arguments)
        {
            string database = "";
            string connectserver = "";
            string connectInfo = "";
            string target = "";
            string intermediate = "";
            string impersonate_linked = "";
            string impersonate_intermediate = "";

            string impersonate = "";

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

            if (!String.IsNullOrEmpty(impersonate) && string.IsNullOrEmpty(target))
            {
                SQLExecutor.ExecuteQuery(connection, $"EXECUTE AS LOGIN = '{impersonate}';");
            }


            var configurations = new string[] {
                "show advanced options",
                "xp_cmdshell",
                "ole automation procedures",
                "clr enabled",
                "clr strict security",
            };

            var queries = new List<string>();
            queries.Add("SELECT name FROM sys.configurations WHERE name = '{0}'");
            queries.Add("SELECT value FROM sys.configurations WHERE name = '{0}'");
            queries.Add("SELECT value_in_use FROM sys.configurations WHERE name = '{0}'");

            foreach (string config in configurations)
            {
                Console.WriteLine("[*] Checking {0} settings on {1}..", config, String.IsNullOrEmpty(target) ? connectserver : target);
                foreach (string query in queries)
                {
                    if (string.IsNullOrEmpty(target) && string.IsNullOrEmpty(intermediate))
                    {
                        SQLExecutor.ExecuteQuery(connection, String.Format(query, config));
                    }
                    else if (string.IsNullOrEmpty(intermediate))
                    {
                        SQLExecutor.ExecuteLinkedQuery(connection, String.Format(query, config), target, impersonate, impersonate_linked);
                    }
                    else
                    {
                        SQLExecutor.ExecuteDoublyLinkedQuery(connection, String.Format(query, config), target, intermediate, impersonate, impersonate_linked, impersonate_intermediate);
                    }
                }
                Console.WriteLine(" -----------------------------------");
            }
            connection.Close();

        }
    }


}
