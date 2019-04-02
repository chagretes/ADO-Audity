using ExtrairSeguranca.Models;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Client;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.Graph;
using Microsoft.VisualStudio.Services.Graph.Client;
using Microsoft.VisualStudio.Services.Security;
using Microsoft.VisualStudio.Services.Security.Client;
using Microsoft.VisualStudio.Services.WebApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ExtrairSeguranca
{
    public class AzureDevOpsAgent
    {
        const string c_collectionUri = "<YOUR COLLECTION URL>";
        public Guid GitId = Guid.Parse("2e9eb7ed-3c0a-47d4-87c1-0ffdd275fd87");
        VssConnection connection;
        static Dictionary<GraphGroup, GroupMemberships> groupCache = new Dictionary<GraphGroup, GroupMemberships>();

        public AzureDevOpsAgent()
        {
            // Interactively ask the user for credentials, caching them so the user isn't constantly prompted
            VssCredentials creds = new VssClientCredentials();
            creds.Storage = new VssClientCredentialStorage();

            // Connect to Azure DevOps Services
            connection = new VssConnection(new Uri(c_collectionUri), creds);
        }

        internal IEnumerable<AccessControlList> ListAccessControl(Guid identify)
        {
            SecurityHttpClient securityClient = connection.GetClient<SecurityHttpClient>();
            IEnumerable<SecurityNamespaceDescription> namespaces = securityClient.QuerySecurityNamespacesAsync(identify).Result;
            IEnumerable<AccessControlList> acls = securityClient.QueryAccessControlListsAsync(
                identify,
                string.Empty,
                descriptors: null,
                includeExtendedInfo: false,
                recurse: true).Result;

            return acls;
        }

        internal List<Permission> GitPermissionsDetails(AccessControlList acessControlList, 
            IEnumerable<GraphUser> users, 
            IEnumerable<GraphGroup> groups,
            List<GitRepository> repositories)
        {
            List<Permission> permissions = new List<Permission>();
            Regex extractEmail = new Regex(@"\\(.*$)");
            Regex extractGuid = new Regex(@".{8}-.{9}-.{4}-.{12}");
            GitHttpClient gitClient = connection.GetClient<GitHttpClient>();
            SecurityHttpClient securityClient = connection.GetClient<SecurityHttpClient>();
            GraphHttpClient graphClient = connection.GetClient<GraphHttpClient>();
           // Guid guid = Guid.Parse(acessControlList.Token.Remove(0, 7));

            IEnumerable<AccessControlList> acls = securityClient.QueryAccessControlListsAsync(
                Guid.Parse("2e9eb7ed-3c0a-47d4-87c1-0ffdd275fd87"),
                acessControlList.Token,
                descriptors: null,
                includeExtendedInfo: false,
                recurse: true).Result;

            // get the details for Git permissions
            Dictionary<int, string> permission = GetGitPermissionNames();

            var tokenGuids = extractGuid.Matches(acessControlList.Token);
            var lastGuidFromToken = Guid.Parse(tokenGuids[tokenGuids.Count-1].Value);
            var repository = repositories.FirstOrDefault(r => r.Id.Equals(lastGuidFromToken));
            var repositoryName = repository != null ? repository.Name : "<none>";

            // use the Git permissions data to expand the ACL
            Console.WriteLine("Expanding ACL for {0} ({1} ACEs)", repositoryName, acessControlList.AcesDictionary.Count());
            foreach (var kvp in acessControlList.AcesDictionary)
            {
                // in the key-value pair, Key is an identity and Value is an ACE (access control entry)
                // allow and deny are bit flags indicating which permissions are allowed/denied
                //PagedGraphUsers group = graphClient.GetGroupAsync(kvp.Key);
                //var user = users.First(u => u.Descriptor.ToString().Equals(kvp.Key.ToString()));

                var group = groups.FirstOrDefault(g => g.Descriptor.Identifier.Equals(kvp.Key.Identifier));
                if (group != null)
                {
                    AddByGroup(permissions, graphClient, permission, repositoryName, kvp, group);
                }
                else
                {
                    AddByUser(users, permissions, extractEmail, permission, repositoryName, kvp);
                }
            }
            return permissions;
        }

        private void AddByUser(IEnumerable<GraphUser> users, List<Permission> permissions, Regex extractEmail, Dictionary<int, string> permission, string repositoryName, KeyValuePair<Microsoft.VisualStudio.Services.Identity.IdentityDescriptor, AccessControlEntry> kvp)
        {
            var user = users.FirstOrDefault(u => extractEmail.Match(kvp.Key.Identifier).Groups[1].Value.Equals(u.MailAddress));
            CreatePermissionList(permissions, permission, repositoryName, kvp, user);
        }

        private void CreatePermissionList(List<Permission> permissions, Dictionary<int, string> permission, string repositoryName, KeyValuePair<Microsoft.VisualStudio.Services.Identity.IdentityDescriptor, AccessControlEntry> kvp, GraphUser user, GraphGroup group = null)
        {
            foreach (var allow in GetPermissionString(kvp.Value.Allow, permission))
            {
                permissions.Add(new Permission
                {
                    Repository = repositoryName,
                    Name = user?.DisplayName,
                    Allow = allow,
                    ID = user.Descriptor.Identifier,
                    Group = group?.DisplayName
                });
            }
            foreach (var deny in GetPermissionString(kvp.Value.Deny, permission))
            {
                permissions.Add(new Permission
                {
                    Repository = repositoryName,
                    Name = user?.DisplayName,
                    Deny = deny,
                    ID = user.Descriptor.Identifier,
                    Group = group?.DisplayName
                });
            }
        }

        private void AddByGroup(List<Permission> permissions, GraphHttpClient graphClient, Dictionary<int, string> permission, string repositoryName, KeyValuePair<Microsoft.VisualStudio.Services.Identity.IdentityDescriptor, AccessControlEntry> kvp, GraphGroup group)
        {
            GroupMemberships expandedMembers = ExpandVSTSGroup(graphClient, group);
            foreach (var user in expandedMembers.Users)
            {
                CreatePermissionList(permissions, permission, repositoryName, kvp, user, group);
            }
        }

        internal PagedGraphUsers GetAllUsers()
        {
            GraphHttpClient graphClient = connection.GetClient<GraphHttpClient>();
            PagedGraphUsers users = graphClient.ListUsersAsync().Result;

            return users;
        }

        internal PagedGraphGroups GetAllGroups()
        {
            GraphHttpClient graphClient = connection.GetClient<GraphHttpClient>();
            PagedGraphGroups groups = graphClient.ListGroupsAsync().Result;

            return groups;
        }

        internal List<GitRepository> GetGitRepositories()
        {
            GitHttpClient gitClient = connection.GetClient<GitHttpClient>();
            return gitClient.GetRepositoriesAsync().Result;
        }

        internal Dictionary<int, string> GetGitPermissionNames()
        {
            SecurityHttpClient securityClient = connection.GetClient<SecurityHttpClient>();

            IEnumerable<SecurityNamespaceDescription> namespaces;
            namespaces = securityClient.QuerySecurityNamespacesAsync(GitId).Result;
            SecurityNamespaceDescription gitNamespace = namespaces.First();

            Dictionary<int, string> permission = new Dictionary<int, string>();
            foreach (ActionDefinition actionDef in gitNamespace.Actions)
            {
                permission[actionDef.Bit] = actionDef.DisplayName;
            }

            return permission;
        }

        internal List<string> GetPermissionString(int bitsSet, Dictionary<int, string> bitMeanings)
        {
            List<string> permissionStrings = new List<string>();
            foreach (var kvp in bitMeanings)
            {
                if ((bitsSet & kvp.Key) == kvp.Key)
                {
                    permissionStrings.Add(kvp.Value);
                }
            }

            if (permissionStrings.Count == 0)
            {
                permissionStrings.Add("<none>");
            }

            return permissionStrings;
        }

        private static GroupMemberships ExpandVSTSGroup(GraphHttpClient graphClient, GraphGroup group)
        {
            groupCache.TryGetValue(group, out GroupMemberships groupMemberships);
            if (groupMemberships != null) return groupMemberships;
            groupMemberships = new GroupMemberships();

            // Convert all memberships into GraphSubjectLookupKeys
            List<GraphSubjectLookupKey> lookupKeys = new List<GraphSubjectLookupKey>();
            List<GraphMembership> memberships = graphClient.ListMembershipsAsync(group.Descriptor, GraphTraversalDirection.Down).Result;
            foreach (var membership in memberships)
            {
                lookupKeys.Add(new GraphSubjectLookupKey(membership.MemberDescriptor));
            }
            IReadOnlyDictionary<SubjectDescriptor, GraphSubject> subjectLookups = graphClient.LookupSubjectsAsync(new GraphSubjectLookup(lookupKeys)).Result;
            foreach (GraphSubject subject in subjectLookups.Values)
            {
                if (subject.OriginId.Equals(group.OriginId)) break; //Father paradox
                switch (subject.Descriptor.SubjectType)
                {
                    //member is an AAD user
                    case "aad":
                        groupMemberships.AddUser((GraphUser)subject);
                        break;

                    //member is an MSA user
                    case "asd2":
                        groupMemberships.AddUser((GraphUser)subject);
                        break;

                    //member is a nested AAD group
                    case "aadgp":
                        groupMemberships.AddAADGroup((GraphGroup)subject);
                        break;

                    //member is a nested VSTS group
                    case "vssgp":
                        GroupMemberships subGroupMemberships = ExpandVSTSGroup(graphClient, (GraphGroup)subject);
                        groupMemberships.Add(subGroupMemberships);
                        break;

                    default:
                        throw new Exception("Unknown SubjectType: " + subject.Descriptor.SubjectType);
                }
            }

            groupCache.Add(group, groupMemberships);
            return groupMemberships;
        }
    }
}
