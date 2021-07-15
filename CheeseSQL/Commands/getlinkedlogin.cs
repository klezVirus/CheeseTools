using System;
using System.Collections.Generic;
using System.Data.SqlClient;


namespace CheeseSQL.Commands
{
    public class getlinkedlogin : ICommand
    {
        public static string CommandName => "getlinkedlogin";

        public string Description()
        {
            return $"[*] {CommandName}\r\n" +
                   $"  Description: Retrieve SQL Logins Available for Impersonation on Linked SQL Servers";
        }

        public string Usage()
        {
            return $"{Description()}\r\n  " +
                $"Usage: {System.Reflection.Assembly.GetExecutingAssembly().GetName().Name} {CommandName} " +
                $"/db:DATABASE " +
                $"/server:SERVER " +
                $"/target:TARGET " +
                $"[/impersonate:USER] " +
                $"[/impersonate-linked:USER] " +
                $"[/sqlauth /user:SQLUSER /password:SQLPASSWORD]";
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

            string queryImp = $"SELECT * FROM OPENQUERY(\"{target}\", 'SELECT distinct b.name FROM sys.server_permissions a INNER JOIN sys.server_principals b ON a.grantor_principal_id = b.principal_id WHERE a.permission_name = ''IMPERSONATE'';')";
            if (!String.IsNullOrEmpty(impersonate_linked))
            {
                queryImp = $"SELECT * FROM OPENQUERY(\"{target}\", 'EXECUTE AS LOGIN = ''{impersonate_linked}'' SELECT distinct b.name FROM sys.server_permissions a INNER JOIN sys.server_principals b ON a.grantor_principal_id = b.principal_id WHERE a.permission_name = ''IMPERSONATE'';')";
            }


            if (!String.IsNullOrEmpty(impersonate))
            {
                queryImp = $"EXECUTE AS LOGIN = '{impersonate}' {queryImp}";
            }

            command = new SqlCommand(queryImp, connection);
            reader = command.ExecuteReader();
            while (reader.Read())
            {
                Console.WriteLine("[*] Login that can be impersonated: {0}", reader[0]);
            }
            reader.Close();

            connection.Close();

        }
    }
}
