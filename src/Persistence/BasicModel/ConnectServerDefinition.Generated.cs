// <copyright file="ConnectServerDefinition.Generated.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

//------------------------------------------------------------------------------
// <auto-generated>
//     This source code was auto-generated by a roslyn code generator.
// </auto-generated>
//------------------------------------------------------------------------------

// ReSharper disable All

namespace MUnique.OpenMU.Persistence.BasicModel;

using MUnique.OpenMU.Persistence.Json;

/// <summary>
/// A plain implementation of <see cref="ConnectServerDefinition"/>.
/// </summary>
public partial class ConnectServerDefinition : MUnique.OpenMU.DataModel.Configuration.ConnectServerDefinition, IIdentifiable, IConvertibleTo<ConnectServerDefinition>
{
    
    
    
    /// <summary>
    /// Gets the raw object of <see cref="Client" />.
    /// </summary>
    [Newtonsoft.Json.JsonProperty("client")]
    [System.Text.Json.Serialization.JsonPropertyName("client")]
    public GameClientDefinition RawClient
    {
        get => base.Client as GameClientDefinition;
        set => base.Client = value;
    }

    /// <inheritdoc/>
    [Newtonsoft.Json.JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    public override MUnique.OpenMU.DataModel.Configuration.GameClientDefinition Client
    {
        get => base.Client;
        set => base.Client = value;
    }


    /// <inheritdoc/>
    public override bool Equals(object obj)
    {
        var baseObject = obj as IIdentifiable;
        if (baseObject != null)
        {
            return baseObject.Id == this.Id;
        }

        return base.Equals(obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return this.Id.GetHashCode();
    }

    /// <inheritdoc/>
    public ConnectServerDefinition Convert() => this;
}
