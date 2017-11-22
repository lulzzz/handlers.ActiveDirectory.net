﻿using System;
using System.DirectoryServices.AccountManagement;
using System.DirectoryServices;

using NUnit.Framework;

using Synapse.ActiveDirectory.Core;

namespace Synapse.ActiveDirectory.Tests.Core
{
    [TestFixture]
    public class GroupManagementTests
    {
        DirectoryEntry workspace = null;
        GroupPrincipal group = null;

        [Test]
        public void Core_GroupManagementTest()
        {
            // Setup Workspace
            workspace = Utility.CreateWorkspace();
            String workspaceName = workspace.Properties["distinguishedName"].Value.ToString();

            // Create Group
            group = Utility.CreateGroup( workspaceName );

            // Add User To Group
            UserPrincipal user = Utility.CreateUser( workspaceName );
            UserPrincipalObject upo = DirectoryServices.GetUser( user.DistinguishedName, true, false, false );
            int beforeCount = upo.Groups.Count;
            Console.WriteLine( $"Adding User [{user.Name}] To Group [{group.Name}]." );
            DirectoryServices.AddUserToGroup( user.DistinguishedName, group.DistinguishedName );
            upo = DirectoryServices.GetUser( user.DistinguishedName, true, false, false );
            int afterCount = upo.Groups.Count;
            Assert.That( afterCount, Is.EqualTo( beforeCount + 1 ) );

            // Remove User From Group
            beforeCount = afterCount;
            Console.WriteLine( $"Removing User [{user.Name}] From Group [{group.Name}]." );
            DirectoryServices.RemoveUserFromGroup( user.DistinguishedName, group.DistinguishedName );
            upo = DirectoryServices.GetUser( user.DistinguishedName, true, false, false );
            afterCount = upo.Groups.Count;
            Assert.That( afterCount, Is.EqualTo( beforeCount - 1 ) );

            // Delete User
            Utility.DeleteUser( user.DistinguishedName );

            // Add Group To Group
            GroupPrincipal newGroup = Utility.CreateGroup( workspaceName );
            GroupPrincipalObject gpo = DirectoryServices.GetGroup( newGroup.DistinguishedName, true, false, false );
            beforeCount = gpo.Groups.Count;
            Console.WriteLine( $"Adding Group [{newGroup.Name}] To Group [{group.Name}]." );
            DirectoryServices.AddGroupToGroup( newGroup.DistinguishedName, group.DistinguishedName );
            gpo = DirectoryServices.GetGroup( newGroup.DistinguishedName, true, false, false );
            afterCount = gpo.Groups.Count;
            Assert.That( afterCount, Is.EqualTo( beforeCount + 1 ) );

            // Remove Group From Group
            beforeCount = afterCount;
            Console.WriteLine( $"Removing Group [{newGroup.Name}] From Group [{group.Name}]." );
            DirectoryServices.RemoveGroupFromGroup( newGroup.DistinguishedName, group.DistinguishedName );
            gpo = DirectoryServices.GetGroup( newGroup.DistinguishedName, true, false, false );
            afterCount = gpo.Groups.Count;
            Assert.That( afterCount, Is.EqualTo( beforeCount - 1 ) );

            // Delete Groups
            Utility.DeleteGroup( newGroup.DistinguishedName );
            Utility.DeleteGroup( group.DistinguishedName );

            // Cleanup Workspace
            Utility.DeleteWorkspace( workspaceName );
        }
    }
}