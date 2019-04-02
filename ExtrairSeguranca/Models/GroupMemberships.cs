using Microsoft.VisualStudio.Services.Graph.Client;
using System.Collections.Generic;
using System.Text;

namespace ExtrairSeguranca.Models
{
    public class GroupMemberships
    {
        public List<GraphUser> Users;
        public List<GraphGroup> AADGroups;

        public GroupMemberships()
        {
            Users = new List<GraphUser>();
            AADGroups = new List<GraphGroup>();
        }

        public void Add(GroupMemberships memberships)
        {
            this.Users.AddRange(memberships.Users);
            this.AADGroups.AddRange(memberships.AADGroups);
        }

        public void AddUser(GraphUser user)
        {
            this.Users.Add(user);
        }

        public void AddAADGroup(GraphGroup group)
        {
            this.AADGroups.Add(group);
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            foreach(var user in Users)
            {
                sb.Append(" - " + user.DisplayName);
            }
            return sb.ToString();
        }
    }
}
