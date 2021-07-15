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
            string user = "";
            string password = "";
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

            string queryLogin = "SELECT SYSTEM_USER;";
            SqlCommand command = new SqlCommand(queryLogin, connection);
            SqlDataReader reader = command.ExecuteReader();
            reader.Read();
            Console.WriteLine("[+] Logged in as: " + reader[0]);
            reader.Close();

            if (!String.IsNullOrEmpty(impersonate))
            {
                string execAs = $"EXECUTE AS LOGIN = '{impersonate}';";
                command = new SqlCommand(execAs, connection);
                reader = command.ExecuteReader();
                reader.Read();
                Console.WriteLine("[*] Attempting impersonation..");
                reader.Close();
            }

            string queryUser = "SELECT USER_NAME();";
            command = new SqlCommand(queryUser, connection);
            reader = command.ExecuteReader();
            reader.Read();
            Console.WriteLine("[+] Mapped to user: " + reader[0]);
            reader.Close();

            string queryPubRole = "SELECT IS_SRVROLEMEMBER('public');";
            command = new SqlCommand(queryPubRole, connection);
            reader = command.ExecuteReader();
            reader.Read();

            Int32 role = Int32.Parse(reader[0].ToString());
            if (role == 1)
            {
                Console.WriteLine("[+] User is a member of the 'Public' role");
            }
            else
            {
                Console.WriteLine("[-] User is not a member of the 'Public' role");
            }
            reader.Close();

            string querySARole = "SELECT IS_SRVROLEMEMBER('sysadmin');";
            command = new SqlCommand(querySARole, connection);

            using (reader = command.ExecuteReader())
            {
                reader.Read();
                role = Int32.Parse(reader[0].ToString());
                if (role == 1)
                {
                    Console.WriteLine("[+] User is a member of the 'sysadmin' role");
                }
                else
                {
                    Console.WriteLine("[-] User is not a member of the 'sysadmin' role");
                }
            }

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
