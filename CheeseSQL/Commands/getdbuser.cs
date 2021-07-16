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
                $"Usage: {System.Reflection.Assembly.GetExecutingAssembly().GetName().Name} {CommandName} /db:DATABASE /server:SERVER [/impersonate:USER] [/permissions] [/sqlauth /user:SQLUSER /password:SQLPASSWORD]";
        }

        public void Execute(Dictionary<string, string> arguments)
        {
            string database = "";
            string connectserver = "";
            string connectInfo = "";

            bool sqlauth = false;
            bool permissions = false;

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
            if (arguments.ContainsKey("/permissions"))
            {
                permissions = true;
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
                SQLExecutor.ExecuteQuery(connection, query);
            }

            /* 
             The following query is quite difficult to wrap within my SQLExecutor, mostly due to the fact I implemented the output in tabular format
             */

            if (permissions)
            {
                Console.WriteLine("[*] Checking user permissions..");

                string queryPermissions = @"SELECT *
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
                SqlCommand command = new SqlCommand(queryPermissions, connection);

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
