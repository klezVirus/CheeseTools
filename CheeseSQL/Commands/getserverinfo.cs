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
                $"Usage: {System.Reflection.Assembly.GetExecutingAssembly().GetName().Name} {CommandName} /db:DATABASE /server:SERVER [/impersonate:USER] [/sqlauth /user:SQLUSER /password:SQLPASSWORD]";
        }

        public void Execute(Dictionary<string, string> arguments)
        {
            string database = "";
            string connectserver = "";
            string connectInfo = "";
            string target = "";
            string impersonate_linked = "";

            bool sqlauth = false;
            string impersonate = "";

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
            if (arguments.ContainsKey("/impersonate-linked"))
            {
                impersonate_linked = arguments["/impersonate-linked"];
            }
            if (arguments.ContainsKey("/server"))
            {
                connectserver = arguments["/server"];
            }
            if (arguments.ContainsKey("/target"))
            {
                target = arguments["/target"];
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
                    if (string.IsNullOrEmpty(target))
                    {
                        SQLExecutor.ExecuteQuery(connection, String.Format(query, config));
                    }
                    else
                    {
                        SQLExecutor.ExecuteLinkedQuery(connection, String.Format(query, config), target, impersonate, impersonate_linked);
                    }
                }
                Console.WriteLine(" -----------------------------------");
            }
            connection.Close();

        }
    }


}
