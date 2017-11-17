﻿using System;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using System.Security.AccessControl;

namespace Synapse.ActiveDirectory.Core
{
    public partial class DirectoryServices
    {
        // Get Access Rules - Retrieves AccessRules Associated With A DirectoryEntry
        public static List<AccessRuleObject> GetAccessRules(Principal principal)
        {
            if ( principal.GetUnderlyingObjectType() == typeof( DirectoryEntry ) )
                return GetAccessRules( (DirectoryEntry)principal.GetUnderlyingObject() );
            else
                throw new AdException( $"GetAccessRules Not Available For Object Type [{principal.GetUnderlyingObjectType()}]", AdStatusType.NotSupported );
        }

        public static List<AccessRuleObject> GetAccessRules(DirectoryEntry de)
        {
            List<AccessRuleObject> accessRules = new List<AccessRuleObject>();
            Dictionary<string, Principal> principals = new Dictionary<string, Principal>();

            AuthorizationRuleCollection rules = de?.ObjectSecurity?.GetAccessRules( true, true, typeof( System.Security.Principal.SecurityIdentifier ) );
            if ( rules != null )
            {
                foreach ( AuthorizationRule rule in rules )
                {
                    ActiveDirectoryAccessRule accessRule = (ActiveDirectoryAccessRule)rule;
                    AccessRuleObject aro = new AccessRuleObject()
                    {
                        ControlType = accessRule.AccessControlType,
                        Rights = accessRule.ActiveDirectoryRights,
                        IdentityReference = accessRule.IdentityReference.Value,
                        InheritanceFlags = accessRule.InheritanceFlags,
                        IsInherited = accessRule.IsInherited,
                    };

                    Principal principal = null;
                    if ( principals.ContainsKey( aro.IdentityReference ) )
                        principal = principals[aro.IdentityReference];
                    else
                    {
                        principal = DirectoryServices.GetPrincipal( aro.IdentityReference );
                        principals.Add( aro.IdentityReference, principal );
                    }

                    aro.IdentityName = principal.Name;
                    accessRules.Add( aro );

                }
            }

            return accessRules;
        }

        // Add Access Rule - Adds Rule For The Given Principal
        public static void AddAccessRule(Principal target, Principal principal, ActiveDirectoryRights rights, AccessControlType type, ActiveDirectorySecurityInheritance inherit = ActiveDirectorySecurityInheritance.None)
        {
            if ( target.GetUnderlyingObjectType() == typeof( DirectoryEntry ) )
                AddAccessRule( (DirectoryEntry)target.GetUnderlyingObject(), principal, rights, type, inherit );
            else
                throw new AdException( $"AddAccessRule Not Available For Object Type [{target.GetUnderlyingObjectType()}]", AdStatusType.NotSupported );
        }

        public static void AddAccessRule(String target, String identity, ActiveDirectoryRights rights, AccessControlType type, ActiveDirectorySecurityInheritance inherit = ActiveDirectorySecurityInheritance.None)
        {
            DirectoryEntry de = GetDirectoryEntry( target );
            AddAccessRule( de, identity, rights, type, inherit );
        }

        public static void AddAccessRule(Principal target, String identity, ActiveDirectoryRights rights, AccessControlType type, ActiveDirectorySecurityInheritance inherit = ActiveDirectorySecurityInheritance.None)
        {
            Principal principal = DirectoryServices.GetPrincipal( identity );
            if ( target.GetUnderlyingObjectType() == typeof( DirectoryEntry ) )
                AddAccessRule( (DirectoryEntry)target.GetUnderlyingObject(), principal, rights, type, inherit );
            else
                throw new AdException( $"AddAccessRule Not Available For Object Type [{target.GetUnderlyingObjectType()}]", AdStatusType.NotSupported );
        }

        public static void AddAccessRule(DirectoryEntry de, String identity, ActiveDirectoryRights rights, AccessControlType type, ActiveDirectorySecurityInheritance inherit = ActiveDirectorySecurityInheritance.None)
        {
            Principal principal = DirectoryServices.GetPrincipal( identity );
            AddAccessRule( de, principal, rights, type );
        }

        public static void AddAccessRule(DirectoryEntry de, Principal principal, ActiveDirectoryRights rights, AccessControlType type, ActiveDirectorySecurityInheritance inherit = ActiveDirectorySecurityInheritance.None)
        {
            ActiveDirectoryAccessRule newRule = new ActiveDirectoryAccessRule( principal.Sid, rights, type, inherit );

            de.ObjectSecurity.AddAccessRule( newRule );
            de.CommitChanges();
        }

        // Delete Access Rule - Deletes Rule For The Principal
        public static void DeleteAccessRule(Principal target, Principal principal, ActiveDirectoryRights rights, AccessControlType type, ActiveDirectorySecurityInheritance inherit = ActiveDirectorySecurityInheritance.None)
        {
            if ( target.GetUnderlyingObjectType() == typeof( DirectoryEntry ) )
                DeleteAccessRule( (DirectoryEntry)target.GetUnderlyingObject(), principal, rights, type, inherit );
            else
                throw new AdException( $"DeleteAccessRule Not Available For Object Type [{target.GetUnderlyingObjectType()}]", AdStatusType.NotSupported );
        }

        public static void DeleteAccessRule(String target, String identity, ActiveDirectoryRights rights, AccessControlType type, ActiveDirectorySecurityInheritance inherit = ActiveDirectorySecurityInheritance.None)
        {
            DirectoryEntry de = GetDirectoryEntry( target );
            DeleteAccessRule( de, identity, rights, type, inherit);
        }

        public static void DeleteAccessRule(Principal target, String identity, ActiveDirectoryRights rights, AccessControlType type, ActiveDirectorySecurityInheritance inherit = ActiveDirectorySecurityInheritance.None)
        {
            Principal principal = DirectoryServices.GetPrincipal( identity );
            if ( target.GetUnderlyingObjectType() == typeof( DirectoryEntry ) )
                DeleteAccessRule( (DirectoryEntry)target.GetUnderlyingObject(), principal, rights, type, inherit );
            else
                throw new AdException( $"DeleteAccessRule Not Available For Object Type [{target.GetUnderlyingObjectType()}]", AdStatusType.NotSupported );
        }

        public static void DeleteAccessRule(DirectoryEntry de, String identity, ActiveDirectoryRights rights, AccessControlType type, ActiveDirectorySecurityInheritance inherit = ActiveDirectorySecurityInheritance.None)
        {
            Principal principal = DirectoryServices.GetPrincipal( identity );
            DeleteAccessRule( de, principal, rights, type, inherit );
        }

        public static void DeleteAccessRule(DirectoryEntry de, Principal principal, ActiveDirectoryRights rights, AccessControlType type, ActiveDirectorySecurityInheritance inherit = ActiveDirectorySecurityInheritance.None)
        {
            ActiveDirectoryAccessRule rule = new ActiveDirectoryAccessRule( principal.Sid, rights, type, inherit );

            de.ObjectSecurity.RemoveAccessRule( rule );
            de.CommitChanges();
        }

        // Set Access Rights - Removes Any Existing Rules For The Principal And Sets Rules To Rights Passed In
        public static void SetAccessRule(Principal target, Principal principal, ActiveDirectoryRights rights, AccessControlType type, ActiveDirectorySecurityInheritance inherit = ActiveDirectorySecurityInheritance.None)
        {
            if ( target.GetUnderlyingObjectType() == typeof( DirectoryEntry ) )
                SetAccessRule( (DirectoryEntry)target.GetUnderlyingObject(), principal, rights, type, inherit );
            else
                throw new AdException( $"SetAccessRule Not Available For Object Type [{target.GetUnderlyingObjectType()}]", AdStatusType.NotSupported );
        }

        public static void SetAccessRule(Principal target, String identity, ActiveDirectoryRights rights, AccessControlType type, ActiveDirectorySecurityInheritance inherit = ActiveDirectorySecurityInheritance.None)
        {
            Principal principal = DirectoryServices.GetPrincipal( identity );
            if ( target.GetUnderlyingObjectType() == typeof( DirectoryEntry ) )
                SetAccessRule( (DirectoryEntry)target.GetUnderlyingObject(), principal, rights, type, inherit );
            else
                throw new AdException( $"SetAccessRule Not Available For Object Type [{target.GetUnderlyingObjectType()}]", AdStatusType.NotSupported );
        }

        public static void SetAccessRule(String target, String identity, ActiveDirectoryRights rights, AccessControlType type, ActiveDirectorySecurityInheritance inherit = ActiveDirectorySecurityInheritance.None)
        {
            DirectoryEntry de = GetDirectoryEntry( target );
            SetAccessRule( de, identity, rights, type, inherit );
        }

        public static void SetAccessRule(DirectoryEntry de, String identity, ActiveDirectoryRights rights, AccessControlType type, ActiveDirectorySecurityInheritance inherit = ActiveDirectorySecurityInheritance.None)
        {
            Principal principal = DirectoryServices.GetPrincipal( identity );
            SetAccessRule( de, principal, rights, type, inherit );
        }

        public static void SetAccessRule(DirectoryEntry de, Principal principal, ActiveDirectoryRights rights, AccessControlType type, ActiveDirectorySecurityInheritance inherit = ActiveDirectorySecurityInheritance.None)
        {
            ActiveDirectoryAccessRule rule = new ActiveDirectoryAccessRule( principal.Sid, rights, type, inherit );

            de.ObjectSecurity.SetAccessRule( rule );
            de.CommitChanges();
        }

        // Purge Access Rights - Removes All Rights For A Given Principal
        public static void PurgeAccessRules(Principal target, Principal principal)
        {
            if ( target.GetUnderlyingObjectType() == typeof( DirectoryEntry ) )
                PurgeAccessRules( (DirectoryEntry)target.GetUnderlyingObject(), principal );
            else
                throw new AdException( $"PurgeAccessRules Not Available For Object Type [{target.GetUnderlyingObjectType()}]", AdStatusType.NotSupported );
        }

        public static void PurgeAccessRules(Principal target, String identity)
        {
            Principal principal = DirectoryServices.GetPrincipal( identity );
            if ( target.GetUnderlyingObjectType() == typeof( DirectoryEntry ) )
                PurgeAccessRules( (DirectoryEntry)target.GetUnderlyingObject(), principal );
            else
                throw new AdException( $"PurgeAccessRules Not Available For Object Type [{target.GetUnderlyingObjectType()}]", AdStatusType.NotSupported );
        }

        public static void PurgeAccessRules(DirectoryEntry de, String identity)
        {
            Principal principal = DirectoryServices.GetPrincipal( identity );
            PurgeAccessRules( de, principal );
        }

        public static void PurgeAccessRules(DirectoryEntry de, Principal principal)
        {
            de.ObjectSecurity.PurgeAccessRules( principal.Sid );
            de.CommitChanges();
        }

    }
}