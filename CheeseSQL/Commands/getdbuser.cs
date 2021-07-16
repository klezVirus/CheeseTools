using CheeseSQL.Helpers;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;

namespace CheeseSQL.Commands
{
    public class getdbuser : ICommand
    {
        public static string CommandName => "getdbuser";
        public string Description()
        {
            return $"[*] {CommandName}\r\n" +
                   $"  Description: Retrieve Information on the SQL Login, Currently Mapped User, and Available User Roles";
        }

        public string Usage()
        {
            return $"{Description()}\r\n  " +
                $"Usage: {System.Reflection.Assembly.GetExecutingAssembly().GetName().Name} {CommandName} " +
                $"/db:DATABASE " +
                $"/server:SERVER " +
                $"[/permissions] " +
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
            string target = "";
            string intermediate = "";
            string connectInfo = "";


            string impersonate = "";
            string impersonate_intermediate = "";
            string impersonate_linked = "";

            bool permissions = arguments.ContainsKey("/permissions");
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
            queries.Add("SELECT IS_SRVROLEMEMBER('public') as 'Public role';");
            queries.Add("SELECT IS_SRVROLEMEMBER('sysadmin') as 'Sysadmin role';");

            foreach (string query in queries)
            {
                if (String.IsNullOrEmpty(target) && String.IsNullOrEmpty(intermediate))
                {
                    SQLExecutor.ExecuteQuery(connection, query);
                }
                else if (String.IsNullOrEmpty(intermediate))
                {
                    SQLExecutor.ExecuteLinkedQuery(connection, query, target, impersonate, impersonate_linked);
                }
                else
                {
                    SQLExecutor.ExecuteDoublyLinkedQuery(connection, query, target, intermediate, impersonate, impersonate_linked, impersonate_intermediate);
                }
            }

            /* 
             The following query is quite difficult to wrap within my SQLExecutor, mostly due to the fact I implemented the output in tabular format
             */

            if (permissions)
            {
                Console.WriteLine("[*] Checking user permissions..");

                string query = @"SELECT *
    FROM(SELECT 'OBJECT' AS entity_class,
                NAME,
                subentity_name,
                permission_name
        FROM   sys.objects
                CROSS APPLY fn_my_permissions(QUOTENAME(NAME), 'OBJECT') a
        UNION ALL
        SELECT 'DATABASE' AS entity_class,
                NAME,
                subentity_name,
                permission_name
        FROM   sys.databases
                CROSS APPLY fn_my_permissions(QUOTENAME(NAME), 'DATABASE') a
        UNION ALL
        SELECT 'SERVER'     AS entity_class,
                @@SERVERNAME AS NAME,
                subentity_name,
                permission_name
        FROM   fn_my_permissions(NULL, 'SERVER')) p
    ORDER  BY entity_class,
            NAME";

                if (!String.IsNullOrEmpty(intermediate) && !String.IsNullOrEmpty(target))
                {
                    query = SQLExecutor.PrepareDoublyLinkedQuery(query, target, intermediate, impersonate, impersonate_linked, impersonate_intermediate);
                }
                else if (!String.IsNullOrEmpty(target))
                {
                    query = SQLExecutor.PrepareLinkedQuery(query, target, impersonate, impersonate_linked);
                }

                SqlCommand command = new SqlCommand(query, connection);

                TablePrinter.PrintRow("ENTITY", "NAME", "SUBENTITY", "PERMISSION");
                TablePrinter.PrintLine();
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        TablePrinter.PrintRow(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3));
                    }
                }
                TablePrinter.PrintLine();
            }
            connection.Close();

        }
    }


}
