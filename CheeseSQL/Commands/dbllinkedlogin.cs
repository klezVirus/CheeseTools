﻿using CheeseSQL.Helpers;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;


namespace CheeseSQL.Commands
{
    public class dbllinkedlogin : ICommand
    {
        public static string CommandName => "dbllinkedlogin";

        public string Description()
        {
            return $"[*] {CommandName}\r\n" +
                   $"  Description: Retrieve SQL Logins Available for Impersonation on Doubly-Linked SQL Servers";
        }

        public string Usage()
        {
            return $"{Description()}\r\n  " +
                $"Usage: {System.Reflection.Assembly.GetExecutingAssembly().GetName().Name} {CommandName} " +
                $"/db:DATABASE " +
                $"/server:SERVER " +
                $"/intermediate:INTERMEDIATE " +
                $"/target:TARGET " +
                $"[/impersonate:USER] " +
                $"[/impersonate-intermediate:USER] " +
                $"[/impersonate-linked:USER] " +
                $"[/sqlauth /user:SQLUSER /password:SQLPASSWORD]";
        }

        public void Execute(Dictionary<string, string> arguments)
        {
            string connectInfo = "";
            string database = "";
            string connectserver = "";
            string intermediate = "";
            string target = "";
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

            var queries = new List<string>();

            queries.Add("SELECT SYSTEM_USER as 'Logged in as', CURRENT_USER as 'Mapped as';");
            queries.Add("SELECT distinct b.name AS 'Login that can be impersonated' FROM sys.server_permissions a INNER JOIN sys.server_principals b ON a.grantor_principal_id = b.principal_id WHERE a.permission_name = 'IMPERSONATE';");

            foreach (string query in queries)
            {
                SQLExecutor.ExecuteDoublyLinkedQuery(connection, query, target, intermediate, impersonate, impersonate_linked, impersonate_intermediate);
            }

            connection.Close();
        }
    }
}
