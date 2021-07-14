using CheeseSQL.Helpers;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;


namespace CheeseSQL.Commands
{
    public class getlinkeddbuser : ICommand
    {
        public static string CommandName => "getlinkeddbuser";

        public string Description()
        {
            return $"[*] {CommandName}\r\n" +
    $"  Description: Retrieve user information on Linked SQL Servers";
        }

        public string Usage()
        {
            return $"{Description()}\r\n  " +
                $"Usage: {System.Reflection.Assembly.GetExecutingAssembly().GetName().Name} {CommandName} /db:DATABASE /server:SERVER /target:TARGET [/permissions] [/impersonate:USER] [/impersonate-linked:USER] [/sqlauth /user:SQLUSER /password:SQLPASSWORD]";
        }

        public void Execute(Dictionary<string, string> arguments)
        {
            string user = "";
            string password = "";
            string connectInfo = "";
            string database = "";
            string connectserver = "";
            string impersonate = "";
            string impersonate_linked = "";
            string target = "";

            bool sqlauth = false;
            bool permissions = false;

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
            }
            if (arguments.ContainsKey("/impersonate-linked"))
            {
                impersonate_linked = arguments["/impersonate-linked"];
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
            if (String.IsNullOrEmpty(target))
            {
                Console.WriteLine("\r\n[X] You must supply a target server!\r\n");
                return;
            }

            if (sqlauth)
            {
                if (arguments.ContainsKey("/user"))
                {
                    user = arguments["/user"];
                }
                if (arguments.ContainsKey("/password"))
                {
                    password = arguments["/password"];
                }
                if (String.IsNullOrEmpty(user))
                {
                    Console.WriteLine("\r\n[X] You must supply the SQL account user!\r\n");
                    return;
                }
                if (String.IsNullOrEmpty(password))
                {
                    Console.WriteLine("\r\n[X] You must supply the SQL account password!\r\n");
                    return;
                }
                connectInfo = "Data Source= " + connectserver + "; Initial Catalog= " + database + "; User ID=" + user + "; Password=" + password;
            }
            else
            {
                connectInfo = "Server = " + connectserver + "; Database = " + database + "; Integrated Security = True;";
            }

            SqlConnection connection = new SqlConnection(connectInfo);

            try
            {
                connection.Open();
                Console.WriteLine($"[+] Authentication to the '{database}' Database on '{connectserver}' Successful!");
            }
            catch
            {
                Console.WriteLine($"[-] Authentication to the '{database}' Database on '{connectserver}' Failed.");
                return;
            }

            string queryLogin = $"SELECT * FROM OPENQUERY(\"{target}\", 'SELECT SYSTEM_USER, CURRENT_USER;')";

            if (!String.IsNullOrEmpty(impersonate_linked))
            {
                queryLogin = $"SELECT * FROM OPENQUERY(\"{target}\", 'EXECUTE AS LOGIN = ''{impersonate_linked}'' SELECT SYSTEM_USER, CURRENT_USER;')";
            }


            if (!String.IsNullOrEmpty(impersonate))
            {
                queryLogin = $"EXECUTE AS LOGIN = '{impersonate}' {queryLogin}";
            }
            SqlCommand command = new SqlCommand(queryLogin, connection);
            SqlDataReader reader = command.ExecuteReader();
            reader.Read();
            Console.WriteLine("[+] Logged in as: {0}, mapped as {1}", reader[0], reader[1]);
            reader.Close();

            if (permissions)
            {
                Console.WriteLine("[*] Checking user permissions..");

                string queryPermissions = $@"SELECT * FROM OPENQUERY(""{target}"", 'SELECT *
FROM(SELECT ''OBJECT'' AS entity_class,
            NAME,
            subentity_name,
            permission_name
    FROM   sys.objects
            CROSS APPLY fn_my_permissions(QUOTENAME(NAME), ''OBJECT'') a
    UNION ALL
    SELECT ''DATABASE'' AS entity_class,
            NAME,
            subentity_name,
            permission_name
    FROM   sys.databases
            CROSS APPLY fn_my_permissions(QUOTENAME(NAME), ''DATABASE'') a
    UNION ALL
    SELECT ''SERVER''     AS entity_class,
            @@SERVERNAME AS NAME,
            subentity_name,
            permission_name
    FROM   fn_my_permissions(NULL, ''SERVER'')) p
ORDER  BY entity_class, NAME');";

                if (!String.IsNullOrEmpty(impersonate_linked))
                {
                    queryPermissions = $@"SELECT * FROM OPENQUERY(""{target}"", 'EXECUTE AS LOGIN = ''{impersonate_linked}''
SELECT *
	FROM   (SELECT ''OBJECT'' AS entity_class,
               NAME,
               subentity_name,
               permission_name
        FROM   sys.objects
               CROSS APPLY fn_my_permissions(QUOTENAME(NAME), ''OBJECT'') a
        UNION ALL
        SELECT ''DATABASE'' AS entity_class,
               NAME,
               subentity_name,
               permission_name
        FROM   sys.databases
               CROSS APPLY fn_my_permissions(QUOTENAME(NAME), ''DATABASE'') a
        UNION ALL
        SELECT ''SERVER''     AS entity_class,
               @@SERVERNAME AS NAME,
               subentity_name,
               permission_name
        FROM   fn_my_permissions(NULL, ''SERVER'')) p
ORDER  BY entity_class,
          NAME');"; 
                }

                if (!String.IsNullOrEmpty(impersonate))
                {
                    queryPermissions = $"EXECUTE AS LOGIN = '{impersonate}' {queryPermissions}";
                }

                command = new SqlCommand(queryPermissions, connection);

                TablePrinter.PrintRow("ENTITY", "NAME", "SUBENTITY", "PERMISSION");
                TablePrinter.PrintLine();
                using (reader = command.ExecuteReader())
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
