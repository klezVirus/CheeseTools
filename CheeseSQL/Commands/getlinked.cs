using CheeseSQL.Helpers;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;


namespace CheeseSQL.Commands
{
    public class getlinked : ICommand
    {
        public static string CommandName => "getlinked";

        public string Description()
        {
            return $"Retrieve Information about Linked Servers";
        }

        public string Usage()
        {
            return $@"{Description()} 
Required arguments:
  /server:SERVER                   Server to connect to

Optional arguments:
  /verbose                         If set, print additional information about the linked server
  /target:TARGET                   Specify a linked SQL server as the target
  /db:DB                           Specify an alternate database to connect 
  /impersonate:USER                Impersonate a user on the connect server
  /impersonate-intermediate:USER   Impersonate a user on the intermediate server
  /impersonate-linked:USER         Impersonate a user on the target server
  /sqlauth                         If set, use SQL authentication
    /user:SQLUSER                  If /sqlauth, set the user for SQL authentication
    /password:SQLPASSWORD          If /sqlauth, set the password for SQL authentication";
        }


        public void Execute(Dictionary<string, string> arguments)
        {
            string connectInfo = "";
            bool verbose;

            ArgumentSet argumentSet;

            try
            {
                argumentSet = ArgumentSet.FromDictionary(
                    arguments,
                    new List<string>() {
                        "/server"
                    });
            }
            catch (Exception e)
            {
                Console.WriteLine($"[x] Error: {e.Message}");
                return;
            }

            argumentSet.GetExtraBool("/verbose", out verbose);


            SqlConnection connection;
            SQLExecutor.ConnectionInfo(arguments, argumentSet.connectserver, argumentSet.database, argumentSet.sqlauth, out connectInfo);
            if (String.IsNullOrEmpty(connectInfo))
            {
                return;
            }
            if (!SQLExecutor.Authenticate(connectInfo, out connection))
            {
                return;
            }

            // I am confused about why it is necessary to perform this step as a separate procedure
            // But it seems in-line impersonation doesn't work properly
            if (!String.IsNullOrEmpty(argumentSet.impersonate))
            {
                Console.WriteLine("[*] Attempting impersonation as {0}", argumentSet.impersonate);
                SQLExecutor.ExecuteProcedure(connection, "", argumentSet.impersonate);
            }

            if (!verbose && String.IsNullOrEmpty(argumentSet.target))
            {
                string procedure = "EXECUTE sp_linkedservers;";
                procedure = SQLExecutor.PrepareSimpleStatement(procedure, argumentSet.impersonate);

                SqlCommand command = new SqlCommand(procedure, connection);

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Console.WriteLine("[*] Linked SQL server: " + reader[0]);
                    }
                }
            }
            else
            {
                string query = "SELECT name, is_linked, is_remote_login_enabled, is_data_access_enabled, is_rpc_out_enabled FROM sys.servers;";
                if (String.IsNullOrEmpty(argumentSet.target) && String.IsNullOrEmpty(argumentSet.intermediate))
                {
                    query = SQLExecutor.PrepareSimpleStatement(
                        query,
                        argumentSet.impersonate
                        );
                }
                else if (String.IsNullOrEmpty(argumentSet.intermediate))
                {
                    query = SQLExecutor.PrepareLinkedQuery(
                        query,
                        argumentSet.target,
                        argumentSet.impersonate,
                        argumentSet.impersonate_linked
                        );

                }
                else
                {
                    query = SQLExecutor.PrepareDoublyLinkedQuery(
                        query,
                        argumentSet.target,
                        argumentSet.intermediate,
                        argumentSet.impersonate,
                        argumentSet.impersonate_linked,
                        argumentSet.impersonate_intermediate
                        );

                }

                SqlCommand command = new SqlCommand(query, connection);

                using (SqlDataReader reader = command.ExecuteReader())
                {

                    while (reader.Read())
                    {
                        Console.Write($@"
[*] SQL server:   {reader.GetString(0)}
    Linked:       {reader.GetBoolean(1).ToString()}
    Remote Login: {reader.GetBoolean(2).ToString()}
    Data Access:  {reader.GetBoolean(3).ToString()}
    RPC out:      {reader.GetBoolean(4).ToString()}
    ----------------------------------------- 
");
                    }
                }

            }
            connection.Close();
        }
    }
}
