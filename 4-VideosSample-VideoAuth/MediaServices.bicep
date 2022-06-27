//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE.md file in the project root for full license information.
//

param baseName string

param location string = resourceGroup().location

param identityName string = '${baseName}identity'

param storageAccountName string = '${baseName}store'

param mediaServicesAccountName string = '${baseName}media'

param contentAwareEncodingTransformName string = 'VideosSampleContentAwareEncodingTransform'

param encryptionWithKeyProxyContentKeyPolicyName string = 'VideosSampleEncryptionWithKeyProxyContentKeyPolicy'

param encryptionWithKeyProxyStreamingPolicyName string = 'VideosSampleEncryptionWithKeyProxyStreamingPolicy'

param drmWithKeyProxyContentKeyPolicyName string = 'VideosSampleDrmWithKeyProxyContentKeyPolicy'

param drmWithKeyProxyStreamingPolicyName string = 'VideosSampleDrmWithKeyProxyStreamingPolicy'

param keyDeliveryProxyBaseUri string = 'https://localhost:7150/'

param tenantId string

param keyDeliveryApplicationClientId string

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
  sku: {
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

// Create a Streaming Policy to allow streaming with encryption and key proxy.
resource encryptionWithKeyProxyContentKeyPolicy 'Microsoft.Media/mediaServices/contentKeyPolicies@2021-11-01' = {
  name: '${mediaServicesAccount.name}/${encryptionWithKeyProxyContentKeyPolicyName}'
  properties: {
    options: [
      {
        name: 'ClearKeyOption'
        configuration: {
          '@odata.type': '#Microsoft.Media.ContentKeyPolicyClearKeyConfiguration'
        }
        restriction: {
          '@odata.type': '#Microsoft.Media.ContentKeyPolicyTokenRestriction'
          issuer: '${environment().authentication.loginEndpoint}${tenantId}/v2.0'
          audience: keyDeliveryApplicationClientId
          requiredClaims: [
            {
              claimType: 'roles'
              claimValue: 'Videos.GetKey'
            }
          ]
          restrictionTokenType: 'Jwt'
          openIdConnectDiscoveryDocument: '${environment().authentication.loginEndpoint}${tenantId}/.well-known/openid-configuration'
        }
      }
    ]
  }
}

// Create a Streaming Policy with encryption and key proxy.
resource encryptionStreamingPolicy 'Microsoft.Media/mediaServices/streamingPolicies@2021-11-01' = {
  name: '${mediaServicesAccount.name}/${encryptionWithKeyProxyStreamingPolicyName}'
  dependsOn: [
    encryptionWithKeyProxyContentKeyPolicy
  ]
  properties: {
    defaultContentKeyPolicyName: encryptionWithKeyProxyContentKeyPolicyName
    envelopeEncryption: {
      enabledProtocols: {
        dash: true
        hls: true
        download: false
        smoothStreaming: false
      }
      customKeyAcquisitionUrlTemplate: '${keyDeliveryProxyBaseUri}envelopeKey?videoId={alternativeMediaId}&contentKeyId={ContentKeyId}'
    }
  }
}

// Create a Content Key Policy with DRM and key proxy.
resource drmWithKeyProxyContentKeyPolicy 'Microsoft.Media/mediaServices/contentKeyPolicies@2021-11-01' = {
  name: '${mediaServicesAccount.name}/${drmWithKeyProxyContentKeyPolicyName}'
  properties: {
    options: [
      {
        name: 'PlayReadyOption'
        configuration: {
          '@odata.type': '#Microsoft.Media.ContentKeyPolicyPlayReadyConfiguration'
          licenses: [
            {
              allowTestDevices: true
              playRight: {
                agcAndColorStripeRestriction: 2
                digitalVideoOnlyContentRestriction: false
                imageConstraintForAnalogComponentVideoRestriction: false
                imageConstraintForAnalogComputerMonitorRestriction: false
                allowPassingVideoContentToUnknownOutput: 'Allowed'
                compressedDigitalAudioOpl: 150
              }
              licenseType: 'NonPersistent'
              contentKeyLocation: {
                '@odata.type': '#Microsoft.Media.ContentKeyPolicyPlayReadyContentEncryptionKeyFromHeader'
              }
              contentType: 'Unspecified'
            }
          ]
        }
        restriction: {
          '@odata.type': '#Microsoft.Media.ContentKeyPolicyTokenRestriction'
          issuer: '${environment().authentication.loginEndpoint}${tenantId}/v2.0'
          audience: keyDeliveryApplicationClientId
          requiredClaims: [
            {
              claimType: 'roles'
              claimValue: 'Videos.GetKey'
            }
          ]
          restrictionTokenType: 'Jwt'
          openIdConnectDiscoveryDocument: '${environment().authentication.loginEndpoint}${tenantId}/.well-known/openid-configuration'
        }
      }
      {
        name: 'WidevineOption'
        configuration: {
          '@odata.type': '#Microsoft.Media.ContentKeyPolicyWidevineConfiguration'
          widevineTemplate: '{"policy_overrides":{"can_persist":false,"can_renew":false,"license_duration_seconds":3000,"rental_duration_seconds":3000,"playback_duration_seconds":1000}}'
        }
        restriction: {
          '@odata.type': '#Microsoft.Media.ContentKeyPolicyTokenRestriction'
          issuer: '${environment().authentication.loginEndpoint}${tenantId}/v2.0'
          audience: keyDeliveryApplicationClientId
          requiredClaims: [
            {
              claimType: 'roles'
              claimValue: 'Videos.GetKey'
            }
          ]
          restrictionTokenType: 'Jwt'
          openIdConnectDiscoveryDocument: '${environment().authentication.loginEndpoint}${tenantId}/.well-known/openid-configuration'
        }
      }
    ]
  }
}

// Create a Streaming Policy with DRM and key proxy.
resource drmWithKeyProxyStreamingPolicy 'Microsoft.Media/mediaServices/streamingPolicies@2021-11-01' = {
  name: '${mediaServicesAccount.name}/${drmWithKeyProxyStreamingPolicyName}'
  dependsOn: [
    drmWithKeyProxyContentKeyPolicy
  ]
  properties: {
    defaultContentKeyPolicyName: drmWithKeyProxyContentKeyPolicyName
    commonEncryptionCenc: {
      enabledProtocols: {
        dash: true
        hls: true
        download: false
        smoothStreaming: false
      }
      drm: {
        playReady: {
          customLicenseAcquisitionUrlTemplate: '${keyDeliveryProxyBaseUri}playReadykey?videoId={alternativeMediaId}'
        }
        widevine: {
          customLicenseAcquisitionUrlTemplate: '${keyDeliveryProxyBaseUri}widevineKey?videoId={alternativeMediaId}&contentKeyId={ContentKeyId}'
        }
      }
    }
  }
}

output subscriptionId string = subscription().id
output resourceGroupName string = resourceGroup().name
output accountName string = mediaServicesAccountName
output keyDeliveryHost string = '${mediaServicesAccountName}.keydelivery.${toLower(replace(mediaServicesAccount.location, ' ', ''))}.media.azure.net'
