using CheeseSQL.Helpers;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;


namespace CheeseSQL.Commands
{
    public class openquery : ICommand
    {
        public static string CommandName => "openquery";

        public string Description()
        {
            return $"[*] {CommandName}\r\n" +
                   $"  Description: Execute an arbitrary query using 'OPENQUERY'";
        }

        public string Usage()
        {
            return $"{Description()}\r\n  " +
                $"Usage: {System.Reflection.Assembly.GetExecutingAssembly().GetName().Name} {CommandName} " +
                $"/db:DATABASE " +
                $"/server:SERVER " +
                $"/target:TARGET " +
                $"/query:QUERY " +
                $"[/intermediate:INTERMEDIATE] " +
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
            string target = "";
            string intermediate = "";
            string query = "";
            string impersonate = "";
            string impersonate_intermediate = "";
            string impersonate_linked = "";

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
            if (!arguments.TryGetValue("/target", out target))
            {
                Console.WriteLine("\r\n[X] You must supply a target server!\r\n");
                return;
            }
            if (!arguments.TryGetValue("/query", out query))
            {
                Console.WriteLine("\r\n[X] You must supply a query to execute!\r\n");
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

            if (String.IsNullOrEmpty(intermediate))
            {
                SQLExecutor.ExecuteLinkedQuery(connection, query, target, impersonate, impersonate_linked);
            }
            else 
            {
                SQLExecutor.ExecuteDoublyLinkedQuery(connection, query, target, intermediate, impersonate, impersonate_linked, impersonate_intermediate);
            }

            connection.Close();
        }
    }
}
