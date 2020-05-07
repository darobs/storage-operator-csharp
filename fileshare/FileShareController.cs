
namespace storage_operator.fileshare
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using k8s.Models;
    using System.Threading.Tasks;
    using Azure.Storage.Files.Shares;
    using k8s;
    using storage_operator.util;
    using Azure.Storage.Files.Shares.Models;
    using System.Linq;
    using System.Collections.Concurrent;
    using storage_operator.fileshare.pv;

    class FileShareController : IFileShareContoller
    {
        readonly IKubernetes k8sClient;
        static readonly KubernetesPvByNameEqualityComparer PvComparer = new KubernetesPvByNameEqualityComparer();

        // Connection String Format:
        // "DefaultEndpointsProtocol=https;AccountName=<>;AccountKey=<>;EndpointSuffix=core.windows.net"
        const string AccountKey = "azurestorageaccountkey";
        const string AccountName = "azurestorageaccountname";
        const string AccessMode = "ReadWriteMany";
        const string StorageClassName = "azurefile";

        public FileShareController(IKubernetes client)
        {
            this.k8sClient = Preconditions.CheckNotNull(client, nameof(client));
        }

        public async Task ManageFileShareSecretAsync(V1Secret secret)
        {
            byte[] accountKeyData;
            byte[] accountNameData;

            if (!secret.Data.TryGetValue(AccountKey, out accountKeyData))
            {
                Console.WriteLine($"Secret {secret.Metadata.Name} doesn't have [{AccountKey}] Data");
                return;
            }
            if (!secret.Data.TryGetValue(AccountName, out accountNameData))
            {
                Console.WriteLine($"Secret {secret.Metadata.Name} doesn't have [{AccountName}] Data");
                return;
            }

            var pvLabels = new Dictionary<string, string>
            {
                [Constants.LabelSelectorKey] = Constants.LabelSelectorValue
            };
            var mountOptions = new List<string>
            {
                "dir_mode=0777",
                "file_mode=0777",
                "uid=1000",
                "gid=1000",
                "mfsymlinks",
                "nobrl"
            };
            V1PersistentVolumeList currentPvs = await k8sClient.ListPersistentVolumeAsync(labelSelector: Constants.LabelSelector);
            var existingPvSet = new Set<V1PersistentVolume>(currentPvs.Items
                .Where(pv => pv.Spec?.AzureFile?.SecretName == secret.Metadata.Name)
                .ToDictionary(pv => pv.Metadata.Name));
            var desiredPvs = new ConcurrentDictionary<string, V1PersistentVolume>();

            string accountKey = Encoding.UTF8.GetString(accountKeyData);
            string accountName = Encoding.UTF8.GetString(accountNameData);
            string connectionString = $"DefaultEndpointsProtocol=https;AccountName={accountName};AccountKey={accountKey};EndpointSuffix=core.windows.net";

            // Open a FileShare client with secret.
            var serviceClient = new ShareServiceClient(connectionString);
            var shares = serviceClient.GetSharesAsync(ShareTraits.Metadata, ShareStates.None);

            await foreach(var share in shares)
            {
                // Get all file shares from client that match a trait
                if ((share.Properties?.Metadata != null) &&
                    (share.Properties.Metadata.TryGetValue(Constants.LabelSelectorKey, out string labelValue)) && 
                    (labelValue == Constants.LabelSelectorValue))
                {
                    // Create a PV from secret and ShareItem
                    Console.WriteLine($"ShareItem {share.Name} found!");
                    string name = KubeUtils.SanitizeK8sValue($"{accountName}-{share.Name}");
                    var metadata = new V1ObjectMeta(name: name, labels: pvLabels);
                    var accessModes = new List<string> { AccessMode };
                    var azurefile = new V1AzureFilePersistentVolumeSource(secret.Metadata.Name, share.Name, readOnlyProperty: false, secret.Metadata.NamespaceProperty);
                    var capacity = new Dictionary<string, ResourceQuantity> { ["storage"] = new ResourceQuantity($"{share.Properties.QuotaInGB}Gi") };
                    var spec = new V1PersistentVolumeSpec(
                            accessModes: accessModes, 
                            azureFile: azurefile, 
                            capacity: capacity, 
                            storageClassName: StorageClassName, 
                            mountOptions: mountOptions);
                    var pv = new V1PersistentVolume(metadata: metadata, spec: spec);
                    if (!desiredPvs.TryAdd(name, pv))
                    {
                        Console.WriteLine($"Duplicate share name {name}");
                    }
                }
            }

            var desiredPvSet = new Set<V1PersistentVolume>(desiredPvs);
            var diff = desiredPvSet.Diff(existingPvSet, PvComparer);
            await this.ManagePvs(diff);
        }

        public async Task DeleteFileShareSecretAsync(V1Secret secret)
        {
            V1PersistentVolumeList currentPvs = await k8sClient.ListPersistentVolumeAsync(labelSelector: Constants.LabelSelector);
            var existingPvSet = new Set<V1PersistentVolume>(currentPvs.Items
                .Where(pv => pv.Spec?.AzureFile?.SecretName == secret.Metadata.Name)
                .ToDictionary(pv => pv.Metadata.Name));
            var desiredPvSet = Set<V1PersistentVolume>.Empty;
            var diff = desiredPvSet.Diff(existingPvSet, PvComparer);
            await this.ManagePvs(diff);
        }


        async Task ManagePvs(Diff<V1PersistentVolume> diff)
        {
            // Skip updated PVs
            // Delete all existing PVs that are not in desired list
            var removingTasks = diff.Removed
                .Select(
                    name =>
                    {
                        Console.WriteLine($"Deleting PV named [{name}].");
                        return this.k8sClient.DeletePersistentVolumeAsync(name: name);
                    });
            await Task.WhenAll(removingTasks);

            // Create new desired PVs
            var addingTasks = diff.Added
                .Select(
                    pv =>
                    {
                        Console.WriteLine($"Creating PV named [{pv.Metadata.Name}].");
                        return this.k8sClient.CreatePersistentVolumeAsync(pv);
                    });
            await Task.WhenAll(addingTasks);
        }
    }
}
