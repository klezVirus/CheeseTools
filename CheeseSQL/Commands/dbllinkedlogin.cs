using System;
using System.Collections.Generic;
using System.Data.SqlClient;


namespace CheeseSQL.Commands
{
    public class dbllinkedlogin : ICommand
    {
        public static string CommandName => "dbllinkedlogin";
        
        public string Description() {
            return $"[*] {CommandName}\r\n" +
                $"  Description: Get Login Information on Doubly Linked SQL Server";
        }

        public string Usage() {
            return $"{Description()}\r\n  " +
                $"Usage: {System.Reflection.Assembly.GetExecutingAssembly().GetName().Name} {CommandName} /db:DATABASE /server:SERVER /intermediate:INTERMEDIATE [/impersonate:USER] [/impersonate-intermediate:USER] [/impersonate-linked:USER] /target:TARGET [/sqlauth /user:SQLUSER /password:SQLPASSWORD]";
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

            string base_query = "SELECT SYSTEM_USER, CURRENT_USER;";
            string loginQuery = $@"SELECT * FROM OPENQUERY(""{intermediate}"", 'SELECT * FROM OPENQUERY (""{target}"", ''{base_query}'')');";

            if (!String.IsNullOrEmpty(impersonate_linked) && !String.IsNullOrEmpty(impersonate_intermediate))
            {
                loginQuery = $@"SELECT * FROM OPENQUERY(""{intermediate}"", 'EXECUTE AS LOGIN = ''{impersonate_intermediate}'' SELECT * FROM OPENQUERY (""{target}"", ''EXECUTE AS LOGIN = ''''{impersonate_linked}'''' {base_query}'')');";
            } else if (!String.IsNullOrEmpty(impersonate_linked)) {
                loginQuery = $@"SELECT * FROM OPENQUERY(""{intermediate}"", 'SELECT * FROM OPENQUERY (""{target}"", ''EXECUTE AS LOGIN = ''''{impersonate_linked}'''' {base_query}'')');";
            }
            else if(!String.IsNullOrEmpty(impersonate_intermediate)) {
                loginQuery = $@"SELECT * FROM OPENQUERY(""{intermediate}"", 'EXECUTE AS LOGIN = ''{impersonate_intermediate}'' SELECT * FROM OPENQUERY (""{target}"", ''{base_query}'')');";
            }

            if (!String.IsNullOrEmpty(impersonate))
            {
                loginQuery = $"EXECUTE AS LOGIN = '{impersonate}' {loginQuery}";
            }
            SqlCommand command = new SqlCommand(loginQuery, connection);
            using (SqlDataReader reader = command.ExecuteReader()) { 
                reader.Read();
                if(reader.FieldCount >= 2) { 
                    Console.WriteLine("[+] Logged in as: {0}, mapped as {1}", reader[0], reader[1]);
                }
            }

            base_query = "SELECT distinct b.name FROM sys.server_permissions a INNER JOIN sys.server_principals b ON a.grantor_principal_id = b.principal_id WHERE a.permission_name = ''''IMPERSONATE'''';";
            
            loginQuery = $@"SELECT * FROM OPENQUERY(""{intermediate}"", 'SELECT * FROM OPENQUERY (""{target}"", ''{base_query}'')');";

            if (!String.IsNullOrEmpty(impersonate_linked) && !String.IsNullOrEmpty(impersonate_intermediate))
            {
                loginQuery = $@"SELECT * FROM OPENQUERY(""{intermediate}"", 'EXECUTE AS LOGIN = ''{impersonate_intermediate}'' SELECT * FROM OPENQUERY (""{target}"", ''EXECUTE AS LOGIN = ''''{impersonate_linked}'''' {base_query}'')');";
            }
            else if (!String.IsNullOrEmpty(impersonate_linked))
            {
                loginQuery = $@"SELECT * FROM OPENQUERY(""{intermediate}"", 'SELECT * FROM OPENQUERY (""{target}"", ''EXECUTE AS LOGIN = ''''{impersonate_linked}'''' {base_query}'')');";
            }
            else if (!String.IsNullOrEmpty(impersonate_intermediate))
            {
                loginQuery = $@"SELECT * FROM OPENQUERY(""{intermediate}"", 'EXECUTE AS LOGIN = ''{impersonate_intermediate}'' SELECT * FROM OPENQUERY (""{target}"", ''{base_query}'')');";
            }

            if (!String.IsNullOrEmpty(impersonate))
            {
                loginQuery = $"EXECUTE AS LOGIN = '{impersonate}' {loginQuery}";
            }
            command = new SqlCommand(loginQuery, connection);
            using (SqlDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    Console.WriteLine("[*] Login that can be impersonated: {0}",  reader.GetString(0));
                }
            }
            connection.Close();
        }
    }
}
