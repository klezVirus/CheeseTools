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
                   $"  Description: Retrieve SQL Logins Available for Impersonation on directly accessible or Linked SQL Servers";
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
            string connectInfo = "";
            string database = "";
            string connectserver = "";
            string intermediate = "";
            string target = "";
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

            if (!String.IsNullOrEmpty(impersonate))
            {
                queries.Add($"EXECUTE AS LOGIN = '{impersonate}';");
            }
            queries.Add("SELECT SYSTEM_USER as 'Logged in as', CURRENT_USER as 'Mapped as';");
            queries.Add("SELECT distinct b.name AS 'Login that can be impersonated' FROM sys.server_permissions a INNER JOIN sys.server_principals b ON a.grantor_principal_id = b.principal_id WHERE a.permission_name = 'IMPERSONATE';");
            queries.Add("SELECT name as 'Owner that can be impersonated', db as 'Trustworthy DB' FROM (SELECT distinct b.name FROM sys.server_permissions a INNER JOIN sys.server_principals b ON a.grantor_principal_id = b.principal_id WHERE a.permission_name = 'IMPERSONATE') impersonable LEFT JOIN (select name AS db, suser_sname( owner_sid ) as owner, is_trustworthy_on from sys.databases) owners ON owners.owner = impersonable.name WHERE is_trustworthy_on = 1;");

            foreach (string query in queries)
            {
                if (String.IsNullOrEmpty(target) && String.IsNullOrEmpty(intermediate)) 
                {
                    SQLExecutor.ExecuteQuery(connection, query);
                } else if (String.IsNullOrEmpty(intermediate)) 
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
