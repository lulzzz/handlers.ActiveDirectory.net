﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Text.RegularExpressions;

using Synapse.Core;
using Synapse.Core.Utilities;
using Synapse.ActiveDirectory.Core;
using Synapse.Handlers.ActiveDirectory;

public class ActiveDirectoryHandler : HandlerRuntimeBase
{
    ActiveDirectoryHandlerConfig config = null;
    HandlerConfig handlerConfig = null;
    IRoleManager roleManager = new DefaultRoleManager();

    HandlerStartInfo startInfo = null;
    string requestUser = null;

    ActiveDirectoryHandlerResults results = new ActiveDirectoryHandlerResults();
    bool isDryRun = false;

    public override IHandlerRuntime Initialize(string config)
    {
        //deserialize the Config from the Handler declaration
        this.config = DeserializeOrNew<ActiveDirectoryHandlerConfig>( config );
        if ( handlerConfig == null )
        {
            this.handlerConfig = HandlerConfig.DeserializeOrNew();
            this.roleManager = AssemblyLoader.Load<IRoleManager>( handlerConfig.RoleManager.Name, @"Synapse.ActiveDirectory.Core:DefaultRoleManager" );
            this.roleManager.Initialize( handlerConfig?.RoleManager?.Config );
        }
        return this;
    }

    public override ExecuteResult Execute(HandlerStartInfo startInfo)
    {
        int cheapSequence = 0;
        const string __context = "Execute";
        ExecuteResult result = new ExecuteResult()
        {
            Status = StatusType.Complete,
            Sequence = int.MaxValue
        };
        string msg = "Complete";
        Exception exc = null;

        this.startInfo = startInfo;
        requestUser = WhoAmI();
        isDryRun = startInfo.IsDryRun;

        try
        {
            //deserialize the Parameters from the Action declaration
            ActiveDirectoryHandlerParameters parameters = YamlHelpers.Deserialize<ActiveDirectoryHandlerParameters>( startInfo.Parameters );

            OnLogMessage( "Execute", $"Running Handler As User [{System.Security.Principal.WindowsIdentity.GetCurrent().Name}]" );
            OnLogMessage( "Execute", $"Request User : [{requestUser}]" );

            //TODO : if IsDryRun == true, test if ConnectionString is valid and works.
            if ( startInfo.IsDryRun )
            {
                OnProgress( __context, "Attempting connection", sequence: cheapSequence++ );


                result.ExitData = "Success";
                result.Message = msg =
                    $"Connection test successful!";

                throw new NotImplementedException("Dry Run Functionality Has Not Yet Been Implemented.");

            }
            else
            {
                switch( config.Action )
                {
                    case ActionType.Get:
                        ProcessActiveDirectoryObjects( parameters.Users, ProcessGet );
                        ProcessActiveDirectoryObjects( parameters.Groups, ProcessGet );
                        ProcessActiveDirectoryObjects( parameters.OrganizationalUnits, ProcessGet );
                        break;
                    case ActionType.Create:
                        ProcessActiveDirectoryObjects( parameters.OrganizationalUnits, ProcessCreate );
                        ProcessActiveDirectoryObjects( parameters.Groups, ProcessCreate );
                        ProcessActiveDirectoryObjects( parameters.Users, ProcessCreate );
                        break;
                    case ActionType.Modify:
                        ProcessActiveDirectoryObjects( parameters.OrganizationalUnits, ProcessModify );
                        ProcessActiveDirectoryObjects( parameters.Groups, ProcessModify );
                        ProcessActiveDirectoryObjects( parameters.Users, ProcessModify );
                        break;
                    case ActionType.Delete:
                        ProcessActiveDirectoryObjects( parameters.Users, ProcessDelete );
                        ProcessActiveDirectoryObjects( parameters.Groups, ProcessDelete );
                        ProcessActiveDirectoryObjects( parameters.OrganizationalUnits, ProcessDelete );
                        break;
                    case ActionType.AddToGroup:
                        ProcessActiveDirectoryObjects( parameters.Users, ProcessGroupAdd );
                        ProcessActiveDirectoryObjects( parameters.Groups, ProcessGroupAdd );
                        break;
                    case ActionType.RemoveFromGroup:
                        ProcessActiveDirectoryObjects( parameters.Users, ProcessGroupRemove );
                        ProcessActiveDirectoryObjects( parameters.Groups, ProcessGroupRemove );
                        break;
                    case ActionType.AddAccessRule:
                        ProcessActiveDirectoryObjects( parameters.Users, ProcessAccessRules );
                        ProcessActiveDirectoryObjects( parameters.Groups, ProcessAccessRules );
                        ProcessActiveDirectoryObjects( parameters.OrganizationalUnits, ProcessAccessRules );
                        break;
                    case ActionType.RemoveAccessRule:
                        ProcessActiveDirectoryObjects( parameters.Users, ProcessAccessRules );
                        ProcessActiveDirectoryObjects( parameters.Groups, ProcessAccessRules );
                        ProcessActiveDirectoryObjects( parameters.OrganizationalUnits, ProcessAccessRules );
                        break;
                    case ActionType.SetAccessRule:
                        ProcessActiveDirectoryObjects( parameters.Users, ProcessAccessRules );
                        ProcessActiveDirectoryObjects( parameters.Groups, ProcessAccessRules );
                        ProcessActiveDirectoryObjects( parameters.OrganizationalUnits, ProcessAccessRules );
                        break;
                    case ActionType.PurgeAccessRules:
                        ProcessActiveDirectoryObjects( parameters.Users, ProcessAccessRules );
                        ProcessActiveDirectoryObjects( parameters.Groups, ProcessAccessRules );
                        ProcessActiveDirectoryObjects( parameters.OrganizationalUnits, ProcessAccessRules );
                        break;
                    case ActionType.AddRole:
                        ProcessActiveDirectoryObjects( parameters.Users, ProcessRoles );
                        ProcessActiveDirectoryObjects( parameters.Groups, ProcessRoles );
                        ProcessActiveDirectoryObjects( parameters.OrganizationalUnits, ProcessRoles );
                        break;
                    case ActionType.RemoveRole:
                        ProcessActiveDirectoryObjects( parameters.Users, ProcessRoles );
                        ProcessActiveDirectoryObjects( parameters.Groups, ProcessRoles );
                        ProcessActiveDirectoryObjects( parameters.OrganizationalUnits, ProcessRoles );
                        break;
                    case ActionType.Search:
                        ProcessSearchRequests( parameters.SearchRequests );
                        break;
                    default:
                        throw new AdException( $"Unknown Action {config.Action} Specified", AdStatusType.NotSupported );
                }
            }
        }
        //something wnet wrong: hand-back the Exception and mark the execution as Failed
        catch ( Exception ex )
        {
            exc = ex;
            result.Status = StatusType.Failed;
            result.ExitData = msg =
                ex.Message + " | " + ex.InnerException?.Message;
        }

        if (string.IsNullOrWhiteSpace(result.ExitData?.ToString()))
            result.ExitData = results.Serialize( config.OutputType, config.PrettyPrint );

        if (!config.SuppressOutput)
            OnProgress( __context, result.ExitData?.ToString(), result.Status, sequence: cheapSequence++, ex: exc );

        //final runtime notification, return sequence=Int32.MaxValue by convention to supercede any other status message
        OnProgress( __context, msg, result.Status, sequence: int.MaxValue, ex: exc );

        return result;
    }

    public override object GetConfigInstance()
    {
        ActiveDirectoryHandlerConfig config = new ActiveDirectoryHandlerConfig();

        config.Action = ActionType.Get;
        config.RunSequential = false;
        config.ReturnGroupMembership = true;
        config.ReturnAccessRules = true;
        config.ReturnObjectProperties = true;
        config.ReturnObjects = true;
        config.SuppressOutput = false;
        config.UseUpsert = true;
        config.OutputType = SerializationFormat.Yaml;
        config.PrettyPrint = true;

        return config;
    }

    public override object GetParametersInstance()
    {
        ActiveDirectoryHandlerParameters parms = new ActiveDirectoryHandlerParameters();

        parms.Users = new List<AdUser>();
        AdUser user = new AdUser();
        user.Identity = "cn=mfox,ou=FamousActors,dc=sandbox,dc=local";
        parms.Users.Add(user);

        parms.Groups = new List<AdGroup>();
        AdGroup group = new AdGroup();
        group.Identity = "cn=BackToTheFuture,ou=Movies,dc=sandbox,dc=local";
        parms.Groups.Add(group);

        parms.OrganizationalUnits = new List<AdOrganizationalUnit>();
        AdOrganizationalUnit ou = new AdOrganizationalUnit();
        ou.Identity = "ou=Movies,dc=sandbox,dc=local";
        parms.OrganizationalUnits.Add(ou);

        parms.SearchRequests = new List<AdSearchRequest>();
        AdSearchRequest search = new AdSearchRequest();
        search.SearchBase = "ou=Synapse,dc=sandbox,dc=local";
        search.Filter = "(objectClass=User)";
        search.ReturnAttributes = new List<string>();
        search.ReturnAttributes.Add("Name");
        search.ReturnAttributes.Add("objectGUID");
        parms.SearchRequests.Add(search);


        return parms;
    }

    private void ProcessActiveDirectoryObjects(IEnumerable<AdObject> objs, Action<AdObject, bool> processFunction)
    {
        if ( objs != null )
        {
            if ( config.RunSequential )
            {
                foreach ( AdObject obj in objs )
                    processFunction( obj, config.ReturnObjects );
            }
            else
                Parallel.ForEach( objs, obj =>
                {
                    processFunction( obj, config.ReturnObjects );
                } );
        }
    }

    private void ProcessGet(AdObject obj, bool returnObject = true)
    {
        ActiveDirectoryObjectResult result = new ActiveDirectoryObjectResult()
        {
            Type = obj.Type,
            Identity = obj.Identity
        };

        try
        {
            roleManager.CanPerformActionOrException( requestUser, ActionType.Get, obj.Identity );
            GetObject( result, obj, returnObject );
        }
        catch (AdException ade)
        {
            ProcessActiveDirectoryException( result, ade, ActionType.Get );
        }

        results.Add( result );
    }

    private void GetObject(ActiveDirectoryObjectResult result, AdObject obj, bool returnObject = true, bool returnStatus = true)
    {
        ActiveDirectoryStatus status = new ActiveDirectoryStatus()
        {
            Action = config.Action,
            Status = AdStatusType.Success,
            Message = "Success",
        };

        try
        {
            object adObject = GetActiveDirectoryObject( obj );
            if ( returnObject )
                result.Object = adObject;
            if ( returnStatus )
                result.Statuses.Add( status );

        }
        catch ( AdException ex )
        {
            ProcessActiveDirectoryException( result, ex, status.Action );
        }
        catch ( Exception e )
        {
            OnLogMessage( "GetObject", e.Message );
            OnLogMessage( "GetObject", e.StackTrace );
            AdException le = new AdException( e );
            ProcessActiveDirectoryException( result, le, status.Action );
        }
    }

    private object GetActiveDirectoryObject(AdObject obj)
    {
        switch ( obj.Type )
        {
            case AdObjectType.User:
                AdUser user = (AdUser)obj;
                UserPrincipalObject upo = null;
                upo = DirectoryServices.GetUser( user.Identity, config.ReturnGroupMembership, config.ReturnAccessRules, config.ReturnObjectProperties );
                if ( upo == null )
                    throw new AdException( $"User [{user.Identity}] Was Not Found.", AdStatusType.DoesNotExist );
                return upo;
            case AdObjectType.Group:
                AdGroup group = (AdGroup)obj;
                GroupPrincipalObject gpo = null;
                gpo = DirectoryServices.GetGroup( group.Identity, config.ReturnGroupMembership, config.ReturnAccessRules, config.ReturnObjectProperties );
                if ( gpo == null )
                    throw new AdException( $"Group [{group.Identity}] Was Not Found.", AdStatusType.DoesNotExist );
                return gpo;
            case AdObjectType.OrganizationalUnit:
                AdOrganizationalUnit ou = (AdOrganizationalUnit)obj;
                OrganizationalUnitObject ouo = null;
                ouo = DirectoryServices.GetOrganizationalUnit( ou.Identity, config.ReturnAccessRules, config.ReturnObjectProperties );
                if ( ouo == null )
                    throw new AdException( $"Organizational Unit [{ou.Identity}] Was Not Found.", AdStatusType.DoesNotExist );
                return ouo;
            default:
                throw new AdException( "Action [" + config.Action + "] Not Implemented For Type [" + obj.Type + "]", AdStatusType.NotSupported );
        }
    }

    private void ProcessCreate(AdObject obj, bool returnObject = true)
    {
        ActiveDirectoryObjectResult result = new ActiveDirectoryObjectResult()
        {
            Type = obj.Type,
            Identity = obj.Identity
        };

        ActiveDirectoryStatus status = new ActiveDirectoryStatus()
        {
            Action = config.Action,
            Status = AdStatusType.Success,
            Message = "Success",
        };

        try
        {
            string statusAction = "Created";

            switch ( obj.Type )
            {
                case AdObjectType.User:
                    AdUser user = (AdUser)obj;
                    UserPrincipal up = null;
                    if ( config.UseUpsert && DirectoryServices.IsExistingUser( obj.Identity ) )
                    {
                        roleManager.CanPerformActionOrException( requestUser, ActionType.Modify, obj.Identity );
                        up = DirectoryServices.GetUserPrincipal( obj.Identity );
                        if ( up == null )
                            throw new AdException( $"User [{obj.Identity}] Not Found.", AdStatusType.DoesNotExist );
                        user.UpdateUserPrincipal( up );
                        statusAction = "Modified";
                    }
                    else if ( DirectoryServices.IsDistinguishedName( obj.Identity ) )
                    {
                        String path = DirectoryServices.GetParentPath( obj.Identity );
                        roleManager.CanPerformActionOrException( requestUser, ActionType.Create, path );
                        up = user.CreateUserPrincipal();
                    }
                    else
                        throw new AdException( $"Identity [{obj.Identity}] Must Be A Distinguished Name For User Creation.", AdStatusType.MissingInput );

                    DirectoryServices.SaveUser( up, isDryRun );
                    OnLogMessage( "ProcessCreate", obj.Type + " [" + obj.Identity + "] " + statusAction + "." );
                    result.Statuses.Add( status );
                    if ( user.Groups != null )
                        AddToGroup( result, user, false );
                    break;
                case AdObjectType.Group:
                    AdGroup group = (AdGroup)obj;
                    GroupPrincipal gp = null;
                    if ( config.UseUpsert && DirectoryServices.IsExistingGroup( obj.Identity ) )
                    {
                        roleManager.CanPerformActionOrException( requestUser, ActionType.Modify, obj.Identity );
                        gp = DirectoryServices.GetGroupPrincipal( obj.Identity );
                        if ( gp == null )
                            throw new AdException( $"Group [{obj.Identity}] Not Found.", AdStatusType.DoesNotExist );
                        group.UpdateGroupPrincipal( gp );
                        statusAction = "Modified";
                    }
                    else if ( DirectoryServices.IsDistinguishedName( obj.Identity ) )
                    {
                        String path = DirectoryServices.GetParentPath( obj.Identity );
                        roleManager.CanPerformActionOrException( requestUser, ActionType.Create, path );
                        gp = group.CreateGroupPrincipal();
                    }
                    else
                        throw new AdException( $"Identity [{obj.Identity}] Must Be A Distinguished Name For Group Creation.", AdStatusType.MissingInput );

                    DirectoryServices.SaveGroup( gp, isDryRun );
                    OnLogMessage( "ProcessCreate", obj.Type + " [" + obj.Identity + "] " + statusAction + "." );
                    result.Statuses.Add( status );
                    if ( group.Groups != null )
                        AddToGroup( result, group, false );

                    break;
                case AdObjectType.OrganizationalUnit:
                    AdOrganizationalUnit ou = (AdOrganizationalUnit)obj;

                    // Get DistinguishedName from User or Group Identity for ManagedBy Property
                    if ( !String.IsNullOrWhiteSpace(ou.ManagedBy) )
                    {
                        if ( ou.Properties == null )
                            ou.Properties = new Dictionary<string, List<string>>();

                        if ( !ou.Properties.ContainsKey( "managedBy" ) )
                        {
                            String distinguishedName = DirectoryServices.GetDistinguishedName( ou.ManagedBy );
                            if ( distinguishedName == null )
                                distinguishedName = ou.ManagedBy;

                            List<String> values = new List<string>() { distinguishedName };
                            ou.Properties.Add( "managedBy", values );
                        }
                    }

                    if ( config.UseUpsert && DirectoryServices.IsExistingDirectoryEntry( obj.Identity ) )
                    {
                        roleManager.CanPerformActionOrException( requestUser, ActionType.Modify, obj.Identity );
                        if ( !String.IsNullOrWhiteSpace( ou.Description ) )
                            DirectoryServices.AddProperty( ou.Properties, "description", ou.Description );
                        DirectoryServices.ModifyOrganizationUnit( ou.Identity, ou.Properties, isDryRun );
                        statusAction = "Modified";
                    }
                    else if ( DirectoryServices.IsDistinguishedName( ou.Identity ) )
                    {
                        String path = DirectoryServices.GetParentPath( obj.Identity );
                        roleManager.CanPerformActionOrException( requestUser, ActionType.Create, path );
                        if ( !String.IsNullOrWhiteSpace( ou.Description ) )
                            DirectoryServices.AddProperty( ou.Properties, "description", ou.Description );
                        DirectoryServices.CreateOrganizationUnit( ou.Identity, ou.Properties, isDryRun );
                    }
                    else
                        throw new AdException( $"Identity [{obj.Identity}] Must Be A Distinguished Name For Organizational Unit Creation.", AdStatusType.MissingInput );

                    OnLogMessage( "ProcessCreate", obj.Type + " [" + obj.Identity + "] " + statusAction + "." );
                    result.Statuses.Add( status );
                    break;
                default:
                    throw new AdException( "Action [" + config.Action + "] Not Implemented For Type [" + obj.Type + "]", AdStatusType.NotSupported );
            }

            if ( returnObject )
                result.Object = GetActiveDirectoryObject( obj );

        }
        catch ( AdException ex )
        {
            ProcessActiveDirectoryException( result, ex, status.Action );
        }
        catch ( Exception e )
        {
            OnLogMessage( "ProcessCreate", e.Message );
            OnLogMessage( "ProcessCreate", e.StackTrace );
            AdException le = new AdException( e );
            ProcessActiveDirectoryException( result, le, status.Action );
        }

        results.Add( result );

    }

    private void ProcessModify(AdObject obj, bool returnObject = true)
    {
        ActiveDirectoryObjectResult result = new ActiveDirectoryObjectResult()
        {
            Type = obj.Type,
            Identity = obj.Identity
        };

        ActiveDirectoryStatus status = new ActiveDirectoryStatus()
        {
            Action = config.Action,
            Status = AdStatusType.Success,
            Message = "Success",
        };

        try
        {
            string statusAction = "Modified";

            switch ( obj.Type )
            {
                case AdObjectType.User:
                    AdUser user = (AdUser)obj;
                    UserPrincipal up = null;
                    if ( config.UseUpsert && !DirectoryServices.IsExistingUser( obj.Identity ) )
                    {
                        if ( DirectoryServices.IsDistinguishedName( obj.Identity ) )
                        {
                            String path = DirectoryServices.GetParentPath( obj.Identity );
                            roleManager.CanPerformActionOrException( requestUser, ActionType.Create, path );
                            up = user.CreateUserPrincipal();
                            statusAction = "Created";
                        }
                        else
                            throw new AdException( $"Identity [{obj.Identity}] Must Be A Distinguished Name For User Creation.", AdStatusType.MissingInput );
                    }
                    else
                    {
                        roleManager.CanPerformActionOrException( requestUser, ActionType.Modify, obj.Identity );
                        up = DirectoryServices.GetUserPrincipal( obj.Identity );
                        if ( up == null )
                            throw new AdException( $"User [{obj.Identity}] Not Found.", AdStatusType.DoesNotExist );
                        user.UpdateUserPrincipal( up );
                    }

                    DirectoryServices.SaveUser( up, isDryRun );

                    OnLogMessage( "ProcessModify", obj.Type + " [" + obj.Identity + "] " + statusAction + "." );
                    if ( user.Groups != null )
                        ProcessGroupAdd( user, false );
                    result.Statuses.Add( status );
                    break;
                case AdObjectType.Group:
                    AdGroup group = (AdGroup)obj;
                    GroupPrincipal gp = null;
                    if ( config.UseUpsert && !DirectoryServices.IsExistingGroup( obj.Identity ) )
                    {
                        if ( DirectoryServices.IsDistinguishedName( obj.Identity ) )
                        {
                            String path = DirectoryServices.GetParentPath( obj.Identity );
                            roleManager.CanPerformActionOrException( requestUser, ActionType.Create, path );
                            gp = group.CreateGroupPrincipal();
                            statusAction = "Created";
                        }
                        else
                            throw new AdException( $"Identity [{obj.Identity}] Must Be A Distinguished Name For Group Creation.", AdStatusType.MissingInput );
                    }
                    else
                    {
                        roleManager.CanPerformActionOrException( requestUser, ActionType.Modify, obj.Identity );
                        gp = DirectoryServices.GetGroupPrincipal( obj.Identity );
                        if ( gp == null )
                            throw new AdException( $"Group [{obj.Identity}] Not Found.", AdStatusType.DoesNotExist );
                        group.UpdateGroupPrincipal( gp );
                    }

                    DirectoryServices.SaveGroup( gp, isDryRun );
                    OnLogMessage( "ProcessModify", obj.Type + " [" + obj.Identity + "] " + statusAction + "." );
                    if ( group.Groups != null )
                        ProcessGroupAdd( group, false );
                    result.Statuses.Add( status );
                    break;
                case AdObjectType.OrganizationalUnit:
                    AdOrganizationalUnit ou = (AdOrganizationalUnit)obj;

                    // Get DistinguishedName from User or Group Identity for ManagedBy Property
                    if ( !String.IsNullOrWhiteSpace( ou.ManagedBy ) )
                    {
                        if ( ou.Properties == null )
                            ou.Properties = new Dictionary<string, List<string>>();

                        if ( !ou.Properties.ContainsKey( "managedBy" ) )
                        {
                            String distinguishedName = DirectoryServices.GetDistinguishedName( ou.ManagedBy );
                            if ( distinguishedName == null )
                                distinguishedName = ou.ManagedBy;

                            List<String> values = new List<string>() { distinguishedName };
                            ou.Properties.Add( "managedBy", values );
                        }
                    }

                    if ( config.UseUpsert && !DirectoryServices.IsExistingDirectoryEntry( obj.Identity ) )
                    {
                        if ( DirectoryServices.IsDistinguishedName( obj.Identity ) )
                        {
                            String path = DirectoryServices.GetParentPath( obj.Identity );
                            roleManager.CanPerformActionOrException( requestUser, ActionType.Create, path );
                            if ( !String.IsNullOrWhiteSpace( ou.Description ) )
                                DirectoryServices.AddProperty( ou.Properties, "description", ou.Description );
                            DirectoryServices.CreateOrganizationUnit( obj.Identity, ou.Properties, isDryRun );
                            statusAction = "Created";
                        }
                        else
                            throw new AdException( $"Identity [{obj.Identity}] Must Be A Distinguished Name For Organizational Unit Creation.", AdStatusType.MissingInput );
                    }
                    else
                    {
                        roleManager.CanPerformActionOrException( requestUser, ActionType.Modify, obj.Identity );
                        if ( !String.IsNullOrWhiteSpace( ou.Description ) )
                            DirectoryServices.AddProperty( ou.Properties, "description", ou.Description );
                        DirectoryServices.ModifyOrganizationUnit( ou.Identity, ou.Properties, isDryRun );
                    }

                    OnLogMessage( "ProcessModify", obj.Type + " [" + obj.Identity + "] " + statusAction + "." );
                    result.Statuses.Add( status );
                    break;
                default:
                    throw new AdException( "Action [" + config.Action + "] Not Implemented For Type [" + obj.Type + "]", AdStatusType.NotSupported );
            }

            if ( returnObject )
                result.Object = GetActiveDirectoryObject( obj );

        }
        catch ( AdException ex )
        {
            ProcessActiveDirectoryException( result, ex, status.Action );
        }
        catch ( Exception e )
        {
            OnLogMessage( "ProcessCreate", e.Message );
            OnLogMessage( "ProcessCreate", e.StackTrace );
            AdException le = new AdException( e );
            ProcessActiveDirectoryException( result, le, status.Action );
        }

        results.Add( result );

    }

    private void ProcessDelete(AdObject obj, bool returnObject = false)
    {
        ActiveDirectoryObjectResult result = new ActiveDirectoryObjectResult()
        {
            Type = obj.Type,
            Identity = obj.Identity
        };

        ActiveDirectoryStatus status = new ActiveDirectoryStatus()
        {
            Action = config.Action,
            Status = AdStatusType.Success,
            Message = "Success",
        };

        try
        {
            roleManager.CanPerformActionOrException( requestUser, ActionType.Delete, obj.Identity );
            switch ( obj.Type )
            {
                case AdObjectType.User:
                    AdUser user = (AdUser)obj;
                    DirectoryServices.DeleteUser( user.Identity );
                    result.Statuses.Add( status );
                    break;
                case AdObjectType.Group:
                    AdGroup group = (AdGroup)obj;
                    DirectoryServices.DeleteGroup( group.Identity, isDryRun );
                    result.Statuses.Add( status );
                    break;
                case AdObjectType.OrganizationalUnit:
                    AdOrganizationalUnit ou = (AdOrganizationalUnit)obj;
                    DirectoryServices.DeleteOrganizationUnit( ou.Identity );
                    result.Statuses.Add( status );
                    break;
                default:
                    throw new AdException( "Action [" + config.Action + "] Not Implemented For Type [" + obj.Type + "]", AdStatusType.NotSupported );
            }

            String message = $"{obj.Type} [{obj.Identity}] Deleted.";
            OnLogMessage( "ProcessDelete", message );
        }
        catch ( AdException ex )
        {
            ProcessActiveDirectoryException( result, ex, status.Action );
        }
        catch ( Exception e )
        {
            OnLogMessage( "ProcessDelete", e.Message );
            OnLogMessage( "ProcessDelete", e.StackTrace );
            AdException le = new AdException( e );
            ProcessActiveDirectoryException( result, le, status.Action );
        }

        results.Add( result );

    }

    private void ProcessGroupAdd(AdObject obj, bool returnObject = true)
    {
        ActiveDirectoryObjectResult result = new ActiveDirectoryObjectResult()
        {
            Type = obj.Type,
            Identity = obj.Identity
        };

        AddToGroup( result, obj, returnObject );
        results.Add( result );
    }

    private void ProcessAccessRules(AdObject obj, bool returnObject = false)
    {
        ActiveDirectoryObjectResult result = new ActiveDirectoryObjectResult()
        {
            Type = obj.Type,
            Identity = obj.Identity
        };

        ActiveDirectoryStatus status = new ActiveDirectoryStatus()
        {
            Action = config.Action,
            Status = AdStatusType.Success,
            Message = "Success",
        };

        try
        {
            roleManager.CanPerformActionOrException( requestUser, config.Action, obj.Identity );

            // Get Target DirectoryEntry For Rules
            DirectoryEntry de = null;
            if ( obj.Type == AdObjectType.User || obj.Type == AdObjectType.Group )
            {
                Principal principal = DirectoryServices.GetPrincipal( obj.Identity );
                if ( principal == null )
                    throw new AdException( $"Principal [{obj.Identity}] Can Not Be Found.", AdStatusType.DoesNotExist );
                else if ( principal.GetUnderlyingObjectType() == typeof( DirectoryEntry ) )
                    de = (DirectoryEntry)principal.GetUnderlyingObject();
                else
                    throw new AdException( $"AddAccessRule Not Available For Object Type [{principal.GetUnderlyingObjectType()}]", AdStatusType.NotSupported );
            }
            else
            {
                de = DirectoryServices.GetDirectoryEntry( obj.Identity );
                if ( de == null )
                    throw new AdException( $"DirectoryEntry [{obj.Identity}] Can Not Be Found.", AdStatusType.DoesNotExist );
            }

            // Add Rules To Target DirectoryEntry
            foreach ( AdAccessRule rule in obj.AccessRules )
            {
                String message = String.Empty;
                switch ( config.Action )
                {
                    case ActionType.AddAccessRule:
                        DirectoryServices.AddAccessRule( de, rule.Identity, rule.Rights, rule.Type );
                        message = $"{rule.Type} [{rule.Rights}] Rule Added To {obj.Type} [{obj.Identity}] For Identity [{rule.Identity}].";
                        break;
                    case ActionType.RemoveAccessRule:
                        DirectoryServices.DeleteAccessRule( de, rule.Identity, rule.Rights, rule.Type );
                        message = $"{rule.Type} [{rule.Rights}] Rule Deleted From {obj.Type} [{obj.Identity}] For Identity [{rule.Identity}].";
                        break;
                    case ActionType.SetAccessRule:
                        DirectoryServices.SetAccessRule( de, rule.Identity, rule.Rights, rule.Type );
                        message = $"{rule.Type} [{rule.Rights}] Rule Set On {obj.Type} [{obj.Identity}] For Identity [{rule.Identity}].";
                        break;
                    case ActionType.PurgeAccessRules:
                        DirectoryServices.PurgeAccessRules( de, rule.Identity );
                        message = $"All Rules Purged On {obj.Type} [{obj.Identity}] For Identity [{rule.Identity}].";
                        break;
                    default:
                        throw new AdException( "Action [" + config.Action + "] Not Implemented For Type [" + obj.Type + "]", AdStatusType.NotSupported );
                }

                result.Statuses.Add( status );
                OnLogMessage( "ProcessAccessRules", message );
            }

            if ( returnObject )
                result.Object = GetActiveDirectoryObject( obj );
        }
        catch ( AdException ex )
        {
            ProcessActiveDirectoryException( result, ex, status.Action );
        }
        catch ( Exception e )
        {
            OnLogMessage( "ProcessDelete", e.Message );
            OnLogMessage( "ProcessDelete", e.StackTrace );
            AdException le = new AdException( e );
            ProcessActiveDirectoryException( result, le, status.Action );
        }

        results.Add( result );

    }

    private void ProcessRoles(AdObject obj, bool returnObject = false)
    {
        ActiveDirectoryObjectResult result = new ActiveDirectoryObjectResult()
        {
            Type = obj.Type,
            Identity = obj.Identity
        };

        ActiveDirectoryStatus status = new ActiveDirectoryStatus()
        {
            Action = config.Action,
            Status = AdStatusType.Success,
            Message = "Success",
        };

        try
        {
            roleManager.CanPerformActionOrException( requestUser, config.Action, obj.Identity );
            foreach (AdRole role in obj.Roles)
            {
                string message = string.Empty;
                switch ( config.Action )
                {
                    case ActionType.AddRole:
                        roleManager.AddRole( role.Principal, role.Name, obj.Identity );
                        message = $"Role [{role.Name}] Has Been Added To {obj.Type} [{obj.Identity}] For Principal [{role.Principal}].";
                        break;
                    case ActionType.RemoveRole:
                        roleManager.RemoveRole( role.Principal, role.Name, obj.Identity );
                        message = $"Role [{role.Name}] Has Been Removed From {obj.Type} [{obj.Identity}] For Principal [{role.Principal}].";
                        break;
                }

                result.Statuses.Add( status );
                OnLogMessage( "ProcessAccessRules", message );
            }

            if ( returnObject )
                result.Object = GetActiveDirectoryObject( obj );

        }
        catch (AdException ade)
        {
            ProcessActiveDirectoryException( result, ade, config.Action );
        }

        results.Add( result );

    }

    private void AddToGroup(ActiveDirectoryObjectResult result, AdObject obj, bool returnObject = true)
    {
        ActiveDirectoryStatus status = new ActiveDirectoryStatus()
        {
            Action = ActionType.AddToGroup,
            Status = AdStatusType.Success,
            Message = "Success",
        };

        try
        {
            switch ( obj.Type )
            {
                case AdObjectType.User:
                    AdUser user = (AdUser)obj;
                    if ( user.Groups != null )
                    {
                        foreach ( string userGroup in user.Groups )
                        {
                            try
                            {
                                roleManager.CanPerformActionOrException( requestUser, ActionType.AddToGroup, userGroup );
                                DirectoryServices.AddUserToGroup( user.Identity, userGroup, isDryRun );
                                String userMessage = $"{obj.Type} [{user.Identity}] Added To Group [{userGroup}].";
                                OnLogMessage( "ProcessGroupAdd", userMessage );
                                status.Message = userMessage;
                                result.Statuses.Add( new ActiveDirectoryStatus( status ) );
                            }
                            catch ( AdException ldeUserEx )
                            {
                                ProcessActiveDirectoryException( result, ldeUserEx, status.Action );
                            }
                        }
                    }
                    break;
                case AdObjectType.Group:
                    AdGroup group = (AdGroup)obj;
                    if ( group.Groups != null )
                    {
                        foreach ( string groupGroup in group.Groups )
                        {
                            try
                            {
                                roleManager.CanPerformActionOrException( requestUser, ActionType.AddToGroup, groupGroup );
                                DirectoryServices.AddGroupToGroup( group.Identity, groupGroup, isDryRun );
                                String groupMessage = $"{obj.Type} [{group.Identity}] Added To Group [{groupGroup}].";
                                OnLogMessage( "ProcessGroupAdd", groupMessage );
                                status.Message = groupMessage;
                                result.Statuses.Add( new ActiveDirectoryStatus( status ) );
                            }
                            catch ( AdException ldeGroupEx )
                            {
                                ProcessActiveDirectoryException( result, ldeGroupEx, status.Action );
                            }
                        }
                    }
                    break;
                default:
                    throw new AdException( "Action [" + config.Action + "] Not Implemented For Type [" + obj.Type + "]", AdStatusType.NotSupported );
            }

            if ( returnObject )
                GetObject( result, obj, true, false );
        }
        catch ( AdException ex )
        {
            ProcessActiveDirectoryException( result, ex, status.Action );
        }
        catch ( Exception e )
        {
            OnLogMessage( "ProcessGroupAdd", e.Message );
            OnLogMessage( "ProcessGroupAdd", e.StackTrace );
            AdException le = new AdException( e );
            ProcessActiveDirectoryException( result, le, status.Action );
        }

    }

    private void ProcessGroupRemove(AdObject obj, bool returnObject = true)
    {
        ActiveDirectoryObjectResult result = new ActiveDirectoryObjectResult()
        {
            Type = obj.Type,
            Identity = obj.Identity
        };

        RemoveFromGroup( result, obj, returnObject );
        results.Add( result );
    }

    private void RemoveFromGroup(ActiveDirectoryObjectResult result, AdObject obj, bool returnObject = true)
    {
        ActiveDirectoryStatus status = new ActiveDirectoryStatus()
        {
            Action = ActionType.RemoveFromGroup,
            Status = AdStatusType.Success,
            Message = "Success",
        };

        try
        {
            switch ( obj.Type )
            {
                case AdObjectType.User:
                    AdUser user = (AdUser)obj;
                    if ( user.Groups != null )
                    {
                        foreach ( string userGroup in user.Groups )
                        {
                            try
                            {
                                roleManager.CanPerformActionOrException( requestUser, ActionType.RemoveFromGroup, userGroup );
                                DirectoryServices.RemoveUserFromGroup( user.Identity, userGroup, isDryRun );
                                String userMessage = $"{obj.Type} [{user.Identity}] Removed From Group [{userGroup}].";
                                OnLogMessage( "ProcessGroupRemove", userMessage );
                                status.Message = userMessage;
                                result.Statuses.Add( new ActiveDirectoryStatus( status ) );
                            }
                            catch (AdException ade)
                            {
                                ProcessActiveDirectoryException( result, ade, status.Action );
                            }
                        }
                    }
                    break;
                case AdObjectType.Group:
                    AdGroup group = (AdGroup)obj;
                    if ( group.Groups != null )
                    {
                        foreach ( string groupGroup in group.Groups )
                        {
                            try
                            {
                                roleManager.CanPerformActionOrException( requestUser, ActionType.RemoveFromGroup, groupGroup );
                                DirectoryServices.RemoveGroupFromGroup( group.Identity, groupGroup, isDryRun );
                                String groupMessage = $"{obj.Type} [{group.Identity}] Removed From Group [{groupGroup}].";
                                OnLogMessage( "ProcessGroupRemove", groupMessage );
                                status.Message = groupMessage;
                                result.Statuses.Add( new ActiveDirectoryStatus( status ) );
                            }
                            catch ( AdException ade )
                            {
                                ProcessActiveDirectoryException( result, ade, status.Action );
                            }

                        }
                    }
                    break;
                default:
                    throw new AdException( "Action [" + config.Action + "] Not Implemented For Type [" + obj.Type + "]", AdStatusType.NotSupported );
            }

            if ( returnObject )
                GetObject( result, obj, true, false );
        }
        catch ( AdException ex )
        {
            ProcessActiveDirectoryException( result, ex, status.Action );
        }
        catch ( Exception e )
        {
            OnLogMessage( "ProcessGroupRemove", e.Message );
            OnLogMessage( "ProcessGroupRemove", e.StackTrace );
            AdException le = new AdException( e );
            ProcessActiveDirectoryException( result, le, status.Action );
        }
    }

    private void ProcessActiveDirectoryException(ActiveDirectoryObjectResult result, AdException ex, ActionType action)
    {
        ActiveDirectoryStatus status = new ActiveDirectoryStatus()
        {
            Action = action,
            Status = ex.Type,
            Message = ex.Message,
        };

        OnLogMessage( "Exception", ex.Message );
        result.Statuses.Add( status );
    }

    private void ProcessSearchRequests(IEnumerable<AdSearchRequest> requests)
    {
        if ( requests != null )
        {
            if ( config.RunSequential )
            {
                foreach ( AdSearchRequest request in requests )
                    ProcessSearchRequest( request );
            }
            else
                Parallel.ForEach( requests, request =>
                {
                    ProcessSearchRequest( request );
                } );
        }
    }

    private void ProcessSearchRequest(AdSearchRequest request)
    {
        ActiveDirectoryStatus status = new ActiveDirectoryStatus()
        {
            Action = ActionType.Search,
            Status = AdStatusType.Success,
            Message = "Success",
        };


        ActiveDirectoryObjectResult result = new ActiveDirectoryObjectResult()
        {
            Type = AdObjectType.SearchResults,
        };

        try
        {
            string searchBase = request.SearchBase;
            if ( String.IsNullOrWhiteSpace( request.SearchBase ) )
                searchBase = DirectoryServices.GetDomainDistinguishedName();
            roleManager.CanPerformActionOrException( requestUser, ActionType.Search, searchBase );

            String filter = request.Filter;
            if ( request.Parameters != null )
                foreach ( RegexParameters param in request.Parameters )
                    filter = Regex.Replace( filter, param.Find, param.ReplaceWith );

            OnLogMessage( "ProcessSearchRequest", $"Executing Search.  Filter String: [{filter}].  Search Base: [{searchBase}]." );
            SearchResultsObject searchResults = DirectoryServices.Search( searchBase, filter, request.ReturnAttributes?.ToArray() );
            result.Object = searchResults;
            result.Statuses.Add( status );
        } 
        catch (AdException ade)
        {
            ProcessActiveDirectoryException( result, ade, ActionType.Search );
        }

        results.Add( result );
    }

    private string WhoAmI()
    {
        string user = System.Security.Principal.WindowsIdentity.GetCurrent().Name;

        if ( DirectoryServices.IsExistingUser( startInfo.RequestUser ) )
            user = startInfo.RequestUser;

        // TODO : Remove Domain Check / Removal Once Support For Multiple Domains Is Added.
        if ( user.Contains( @"\" ) )
            user = user.Substring( user.IndexOf(@"\") + 1 );

        return user;
    }
}