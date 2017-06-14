﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Synapse.Ldap.Core
{
    public class ConnectionInfo
    {
        public string LdapRoot { get; set; }
    }

    public class LdapHandlerParameters
    {
        public ActionType Action { get; set; } = ActionType.Query;
        public ObjectClass Type { get; set; }
        public SerializationFormat ReturnFormat { get; set; } = SerializationFormat.Json;
    }

    public class SecurityPrincipalQueryParameters : LdapHandlerParameters
    {
        public string Name { get; set; } = "kitten";
        public bool IncludeGroups { get; set; }
    }
}