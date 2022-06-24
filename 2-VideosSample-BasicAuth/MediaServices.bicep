//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE.md file in the project root for full license information.
//

param baseName string

param location string = resourceGroup().location

param identityName string ='${baseName}identity'

param storageAccountName string = '${baseName}store'

param mediaServicesAccountName string ='${baseName}media'

param contentAwareEncodingTransformName string = 'VideosSampleContentAwareEncodingTransform'

param encryptionContentKeyPolicyName string = 'VideosSampleEncryptionContentKeyPolicy'

param encryptionStreamingPolicyName string = 'VideosSampleEncryptionStreamingPolicy'

param tenantId string

param apiApplicationClientId string

var storageBlobDataContributorRole = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'

// Create a Managed Identity to allow Media Services to access the storage account.
resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2018-11-30' = {
  name: identityName
  location: location
}

// Create a storage account to store media assets.
resource storageAccount 'Microsoft.Storage/storageAccounts@2021-08-01' = {
  name: storageAccountName
  location: location
  sku:{
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
}

// Grant the Managed Identity access to the storage account.
resource roleAssignment 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = {
  name: guid(managedIdentity.id, storageAccount.id, storageBlobDataContributorRole)
  scope: storageAccount
  properties: {
    principalId: managedIdentity.properties.principalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataContributorRole)
    principalType: 'ServicePrincipal'
  }
}

// Create a Media Services account linked to the storage account, using the Managed Identity for authentication.
resource mediaServicesAccount 'Microsoft.Media/mediaservices@2021-11-01' = {
  name: mediaServicesAccountName
  location: location
  properties: {
    storageAccounts: [
      {
        type: 'Primary'
        id: storageAccount.id
        identity: {
          userAssignedIdentity: managedIdentity.id
          useSystemAssignedIdentity: false
        }
      }
    ]
    storageAuthentication: 'ManagedIdentity'
  }
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentity.id}': {}
    }
  }
}

// Create a transform to prepare media for streaming.
resource transform 'Microsoft.Media/mediaServices/transforms@2021-11-01' = {
  name: '${mediaServicesAccount.name}/${contentAwareEncodingTransformName}'
  properties: {
    outputs: [
      {
        preset: {
          '@odata.type':'#Microsoft.Media.BuiltInStandardEncoderPreset'
          presetName: 'ContentAwareEncoding'
        }
      }
    ]
  }
}

// Create a Streaming Policy to allow streaming with encryption.
resource encryptionContentKeyPolicy 'Microsoft.Media/mediaServices/contentKeyPolicies@2021-11-01' = {
  name: '${mediaServicesAccount.name}/${encryptionContentKeyPolicyName}'
  properties: {
    options: [
      {
        name: 'ClearKeyOption'
        configuration: {
          '@odata.type': '#Microsoft.Media.ContentKeyPolicyClearKeyConfiguration'
        }
        restriction: {
          '@odata.type': '#Microsoft.Media.ContentKeyPolicyTokenRestriction'
          issuer: 'https://login.microsoftonline.com/${tenantId}/v2.0'
          audience: apiApplicationClientId
          requiredClaims: [
            {
              claimType: 'scp'
              claimValue: 'Videos.Watch'
            }
          ]
          restrictionTokenType: 'Jwt'
          openIdConnectDiscoveryDocument: 'https://login.windows.net/${tenantId}/.well-known/openid-configuration'
        }
      }
    ]
  }
}

// Create a Streaming Policy with encryption.
resource encryptionStreamingPolicy 'Microsoft.Media/mediaServices/streamingPolicies@2021-11-01' = {
  name: '${mediaServicesAccount.name}/${encryptionStreamingPolicyName}'
  dependsOn: [
    encryptionContentKeyPolicy
  ]
  properties: {
    defaultContentKeyPolicyName: encryptionContentKeyPolicyName
    envelopeEncryption: {
      enabledProtocols: {
        dash: true
        hls: true
        download: false
        smoothStreaming: false
      }
    }
  }
}


output subscriptionId string = subscription().id
output resourceGroupName string = resourceGroup().name
output accountName string = mediaServicesAccountName
