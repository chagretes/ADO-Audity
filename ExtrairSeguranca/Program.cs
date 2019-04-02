using CsvHelper;
using ExtrairSeguranca.Models;
using System;
using System.Collections.Generic;
using System.IO;

namespace ExtrairSeguranca
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Extraindo Informações do Azure DevOps");
            AzureDevOpsAgent azureDevOpsAgent = new AzureDevOpsAgent();
            GenerateGitReport(azureDevOpsAgent);
            Console.ReadKey();
        }

        static void GenerateGitReport(AzureDevOpsAgent agent)
        {

            List<Permission> permissions = new List<Permission>();

            var users = agent.GetAllUsers().GraphUsers;
            var groups = agent.GetAllGroups().GraphGroups;
            var repositories = agent.GetGitRepositories();
            var accesses = agent.ListAccessControl(agent.GitId);

            foreach(var access in accesses)
            {
                permissions.AddRange(agent.GitPermissionsDetails(access, users, groups, repositories));
            }

            using (var writer = new StreamWriter("[AzureDevOps] Acessos Git.csv"))
            using (var csv = new CsvWriter(writer))
            {
                csv.WriteRecords(permissions);
            }
        }
    }
}
