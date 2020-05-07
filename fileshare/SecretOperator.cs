
namespace storage_operator.fileshare
{
    using System;
    using System.Threading.Tasks;
    using k8s;
    using k8s.Models;
    using Microsoft.Rest;
    using storage_operator.util;


    class SecretOperator : IKubernetesOperator
    {
        readonly IKubernetes client;
        readonly IFileShareContoller controller;
        Option<Watcher<V1Secret>> operatorWatch;

        public SecretOperator(IKubernetes client, IFileShareContoller controller)
        {
            this.client = Preconditions.CheckNotNull(client, nameof(client));
            this.controller = Preconditions.CheckNotNull(controller, nameof(controller));
        }

        public void Start() => this.StartListSecrets();

        public void Stop()
        {
            this.operatorWatch.ForEach(watch => watch.Dispose());
        }
        public void Dispose() => this.Stop();

        void StartListSecrets() =>
            this.client.ListSecretForAllNamespacesWithHttpMessagesAsync(watch: true, labelSelector: Constants.LabelSelector)
            .ContinueWith(this.OnSecretsListCompleted);

        async Task OnSecretsListCompleted(Task<HttpOperationResponse<V1SecretList>> task)
        {
            HttpOperationResponse<V1SecretList> response = await task;

            this.operatorWatch = Option.Some(
                response.Watch<V1Secret, V1SecretList>(
                onEvent: async (type, item) => await this.SecretOnEventHandlerAsync(type, item),
                onClosed: () =>
                {
                    // get rid of the current edge deployment watch object since we got closed
                    this.operatorWatch.ForEach(watch => watch.Dispose());
                    this.operatorWatch = Option.None<Watcher<V1Secret>>();

                    // kick off a new watch
                    this.StartListSecrets();
                },
                onError: (e) =>
                {
                    Console.WriteLine("Error in Secret watch:");
                    throw e;
                }));
        }

        internal async Task SecretOnEventHandlerAsync(WatchEventType type, V1Secret secret)
        {
            Console.WriteLine($"Secret {secret.Metadata.Name} {type}");

            switch (type)
            {
                case WatchEventType.Added:
                case WatchEventType.Modified:
                    await this.controller.ManageFileShareSecretAsync(secret);
                    break;

                case WatchEventType.Deleted:
                    await this.controller.DeleteFileShareSecretAsync(secret);
                    break;

                case WatchEventType.Error:
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }
    }
}
