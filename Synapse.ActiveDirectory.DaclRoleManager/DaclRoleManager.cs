﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;

using Synapse.ActiveDirectory.Core;

namespace Synapse.ActiveDirectory.RoleManager
{
    public class DaclRoleManager : IRoleManager
    {
        Dictionary<string, DaclRole> Roles = new Dictionary<string, DaclRole>();
        Dictionary<ActionType, List<string>> Actions = new Dictionary<ActionType, List<string>>();

        public DaclRoleManager()
        {
            DaclRoleManagerConfig config = DaclRoleManagerConfig.Load( @"C:\Source\handlers.ActiveDirectory.net\Synapse.ActiveDirectory.DaclRoleManager\bin\Debug\RoleManagerConfig.yaml" );
            foreach (DaclRole role in config.Roles)
            {
                Roles.Add( role.Name, role );
                UpdateAllowedActions( role.Name, role.AllowedActions );
            }

            // Load "Role Inheritance" Values
            foreach (DaclRole role in config.Roles)
            {
                if ( role.ExtendsRoles != null )
                {
                    foreach ( string parent in role.ExtendsRoles )
                    {
                        if ( Roles.ContainsKey( parent ) )
                            UpdateAllowedActions( role.Name, Roles[parent].AllowedActions );
                    }
                }
            }
        }

        private void UpdateAllowedActions(string roleName, ActionType allowedActions)
        {
            ActionType[] actionTypes = (ActionType[])(Enum.GetValues( typeof( ActionType ) ));
            foreach ( ActionType action in actionTypes )
            {
                if ( action != ActionType.All && action != ActionType.None )
                {
                    if ( !Actions.ContainsKey( action ) )
                    {
                        List<string> roles = new List<string>();
                        Actions.Add( action, roles );
                    }

                    if ( (allowedActions & action) == action )
                        Actions[action].Add( roleName );
                }
            }
        }

        #region Role Execution

        public bool CanPerformAction(string principal, ActionType action, string adObject)
        {
            bool canPerformAction = false;
            ActiveDirectoryRights principalRights = GetAdAccessRights( principal, adObject );

            foreach (string role in Actions[action])
            {
                ActiveDirectoryRights roleRights = Roles[role].AdRights;
                if ((roleRights & principalRights) == roleRights )
                {
                    canPerformAction = true;
                    break;
                }
            }

            return canPerformAction;
        }

        public void CanPerformActionOrException(string principal, ActionType action, string adObject)
        {
            bool rc = CanPerformAction( principal, action, adObject );
            if ( !rc )
                throw new AdException( $"[{principal}] cannot perform action [{action}] on [{adObject}].", AdStatusType.NotAllowed );
        }

        #endregion

        #region Role Administration

        public void AddRole(string principal, string role, string adObject)
        {
            Principal p = DirectoryServices.GetPrincipal( principal );
            DirectoryEntry target = DirectoryServices.GetDirectoryEntry( adObject );

            if ( Roles.ContainsKey( role ) )
                DirectoryServices.AddAccessRule( target, p, Roles[role].AdRights, System.Security.AccessControl.AccessControlType.Allow );
            else
                throw new AdException( $"Role [{role}] Does Not Exist.", AdStatusType.DoesNotExist );
        }

        public IEnumerable<string> GetRoles()
        {
            return Roles.Keys.ToList<string>();
        }

        public bool HasRole(string principal, string role, string adObject)
        {
            ActiveDirectoryRights principalRights = GetAdAccessRights( principal, adObject );
            ActiveDirectoryRights roleRights = Roles[role].AdRights;

            bool hasRole = (principalRights & roleRights) == roleRights;
            return hasRole;
        }

        public void RemoveRole(string principal, string role, string adObject)
        {
            Principal p = DirectoryServices.GetPrincipal( principal );
            DirectoryEntry target = DirectoryServices.GetDirectoryEntry( adObject );

            if ( Roles.ContainsKey( role ) )
                DirectoryServices.DeleteAccessRule( target, p, Roles[role].AdRights, System.Security.AccessControl.AccessControlType.Allow );
            else
                throw new AdException( $"Role [{role}] Does Not Exist.", AdStatusType.DoesNotExist );
        }

        #endregion

        // Get A Principal's Cumulitive AD Rights On An Object
        private ActiveDirectoryRights GetAdAccessRights(string principal, string adObject)
        {
            ActiveDirectoryRights myRights = 0;
            Principal p = DirectoryServices.GetPrincipal( principal );
            List<DirectoryEntry> groups = DirectoryServices.GetGroupMembership( p, true );

            DirectoryEntry de = DirectoryServices.GetDirectoryEntry( adObject );
            List<AccessRuleObject> rules = DirectoryServices.GetAccessRules( de );

            Dictionary<string, ActiveDirectoryRights> rights = new Dictionary<string, ActiveDirectoryRights>();

            foreach (AccessRuleObject rule in rules)
            {
                if ( rights.Keys.Contains( rule.IdentityReference ) )
                {
                    rights[rule.IdentityReference] |= rule.Rights;
                }
                else
                    rights.Add( rule.IdentityReference, rule.Rights );
            }

            foreach (DirectoryEntry entry in groups)
            {
                if ( entry.Properties.Contains( "objectSid" ) )
                {
                    string sid = DirectoryServices.ConvertByteToStringSid( (byte[])entry.Properties["objectSid"].Value );
                    if ( rights.ContainsKey( sid ) )
                        myRights |= rights[sid];
                }
            }

            return myRights;

        }
    }
}
