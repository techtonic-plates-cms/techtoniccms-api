using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HotChocolate;
using HotChocolate.Configuration;
using HotChocolate.Data;
using HotChocolate.Types;
using HotChocolate.Types.Descriptors;
using HotChocolate.Types.Descriptors.Definitions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TechtonicCmsApi.Contexts;
using TechtonicCmsApi.Schema.TechtonicCms.Entities;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;
using TechtonicCmsApi.Services;

namespace TechtonicCmsApi.Types.Collections.DynamicCollections;

public class CollectionConnectionTypeInterceptor : TypeInterceptor
{
    public override void OnBeforeCompleteType(
        ITypeCompletionContext context,
        DefinitionBase definition)
    {
        if (definition is not ObjectTypeDefinition typeDef)
            return;
        if (typeDef.Name.EndsWith("EntryConnection"))
        {
            var prefix = typeDef.Name[..^"Connection".Length];
            var nodeField = typeDef.Fields.FirstOrDefault(f => f.Name == "nodes");
            if (nodeField != null)
            {
                nodeField.Type = TypeReference.Parse($"[{prefix}]");
            }
            return;
        }

        if (typeDef.Name.EndsWith("EntryEdge"))
        {
            var prefix = typeDef.Name[..^"Edge".Length];
            var nodeField = typeDef.Fields.FirstOrDefault(f => f.Name == "node");
            if (nodeField != null)
            {
                nodeField.Type = TypeReference.Parse(prefix);
            }
            
        }
    }
}