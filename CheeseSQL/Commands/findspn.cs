using CheeseSQL.Commands;
using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.Linq;

namespace CheeseSQL
{

    public class findspn : ICommand
    {
        public static string CommandName => "findspn";
        public string Description()
        {
            return $"[*] {CommandName}\r\n" +
    $"  Description: Find MSSQL Instances, using Domain SPNs";
        }

        public string Usage()
        {
            return $"{Description()}\r\n  " +
                $"Usage: {System.Reflection.Assembly.GetExecutingAssembly().GetName().Name} {CommandName} /ldapPath:LDAP_PATH /account:FILTER /computer";
        }

        public void Execute(Dictionary<string, string> arguments)
        {
            var domainController = string.Format("LDAP://{0}", Environment.GetEnvironmentVariable("logonserver").Replace("\\", string.Empty));
            string distinguishedName;
            DirectoryEntry directoryEntry;
            string spnFilter = string.Empty;
            SearchResultCollection resultCollection;

            string _filter = null;

            string ldapPath = null;
            if (arguments.ContainsKey("/ldapPath")) {
                arguments.TryGetValue("/ldapPath", out ldapPath);
            }
            if (arguments.ContainsKey("/account")) {
                arguments.TryGetValue("/account", out _filter);
            }

            if (!String.IsNullOrEmpty(_filter) && arguments.ContainsKey("/computer")) {
                spnFilter = GetComputerAccountFilter(_filter);
            }
            else if (!String.IsNullOrEmpty(_filter)) {
                spnFilter = GetDomainAccountFilter(_filter);
            }

            string filter = string.Format("(&(servicePrincipalName={0}){1})", "MSSQL*", spnFilter);


            try
            {
                directoryEntry = new DirectoryEntry(domainController);
                distinguishedName = (string)directoryEntry.Properties["distinguishedName"].Value;
                if (!string.IsNullOrEmpty(ldapPath))
                {
                    ldapPath = string.Format("/{0},{1}", ldapPath, distinguishedName);
                    directoryEntry = new DirectoryEntry(domainController + ldapPath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("{0} : {1}", domainController, ex.Message);
                return;
            }

            var directorySearcher = new DirectorySearcher(directoryEntry)
            {
                SizeLimit = 10,
                Filter = filter,
                SearchScope = SearchScope.Subtree
            };

            try
            {
                resultCollection = directorySearcher.FindAll();
            }
            catch (Exception ex)
            {
                if (ex is InvalidOperationException)
                    Console.WriteLine("{0} : Invalid Operation Exception Occured: {1}", domainController, ex.Message);
                else if (ex is NotSupportedException)
                    Console.WriteLine("{0} : Not Supported Exception Occured: {1}", domainController, ex.Message);
                else
                    Console.WriteLine("{0} : {1}", domainController, ex.Message);
                return;
            }
            // Print found MSSQL instances
            ParseCollection(resultCollection);

        }

        protected string GetDomainAccountFilter(string accountName)
        {
            return string.Format("(objectcategory=person)(SamAccountName={0})", accountName);
        }

        protected string GetComputerAccountFilter(string accountName)
        {
            return string.Format("(objectcategory=computer)(SamAccountName={0}$)", accountName);
        }

        internal bool ParseCollection(SearchResultCollection resultCollection)
        {
            Console.WriteLine("SPNs Returned : {0}", resultCollection.Count);
            foreach (SearchResult item in resultCollection)
            {
                foreach (string spn in item.Properties["ServicePrincipalName"])
                {
                    string spnService = spn.Split(new string[] { "/" }, StringSplitOptions.RemoveEmptyEntries).First();
                    if (!spnService.Contains("MSSQL"))
                        continue;

                    string spnServer = spn.Split(new string[] { "/" }, StringSplitOptions.RemoveEmptyEntries).Last()
                        .Split(new string[] { ":" }, StringSplitOptions.RemoveEmptyEntries).First()
                        .Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries).First();

                    string instance = spn.Split(new string[] { ":" }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();

                    int port = 0;
                    string serverInstance = spn.Split(new string[] { "/" }, StringSplitOptions.RemoveEmptyEntries).Last()
                        .Replace(':', int.TryParse(instance, out port) ? ',' : '\\');

                    DateTime lastLogon = DateTime.MinValue;

                    if (null != item.Properties["lastlogontimestamp"])
                    {
                        if(item.Properties["lastlogontimestamp"].Count > 0) { 
                            lastLogon = DateTime.FromFileTime((long)item.Properties["lastlogontimestamp"][0]);
                        }
                    }
                    string samAccountName = "";
                    if (null != item.Properties["samaccountname"]) {
                        if (item.Properties["samaccountname"].Count > 0) {
                            samAccountName = item.Properties["samaccountname"][0].ToString();
                        }
                    }

                    string userCN = "";
                    if (null != item.Properties["cn"]) {
                        if (item.Properties["cn"].Count > 0) {
                            samAccountName = item.Properties["cn"][0].ToString();
                        }
                    }
                    
                    string description = "";
                    if (null != item.Properties["description"]) {
                        if (item.Properties["description"].Count > 0) {
                            description = item.Properties["description"][0].ToString();
                        }
                    }

                    string userSid = "";
                    if (null != item.Properties["objectsid"]) {
                        if (item.Properties["objectsid"].Count > 0) {
                            if (item.Properties["objectsid"][0].GetType().Equals(typeof(byte[]))) { 
                                userSid = BitConverter.ToString((byte[])item.Properties["objectsid"][0]).Replace("-", "");
                            } else if (item.Properties["objectsid"][0].GetType().Equals(typeof(string))) {
                                userSid = item.Properties["objectsid"][0].ToString();
                            }
                        }
                    }


                        Console.Write($@"
SPN:            {spn}
ServerInstance: {serverInstance}
Server:         {spnServer}
Service:        {spnService}
UserSid:        {userSid}
User:           {samAccountName}
UserCN:         {userCN}
LastLogon:      {lastLogon}
Description:    {description}
 ---------------------------------
                ");
                }
            }
            return true;
        }
    }
}
