

namespace storage_operator
{
    using System;
    using System.IO;
    using System.Runtime.Loader;
    using System.Threading;
    using System.Threading.Tasks;
    using k8s;
    using storage_operator.fileshare;
    using storage_operator.util;

    class Program
    {

        static async Task<int> MainAsync()
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            LinuxShutdownHandler.Init(cts);

            var client = GetK8sClient();
            var fsc = GetFileShareController(client);

            var secretOperator = new SecretOperator(client, fsc);
            secretOperator.Start();

            while (!cts.Token.IsCancellationRequested)
            {
                // Main loop can potentially do resolution of Azure FileShare updates.
                await Task.Delay(TimeSpan.FromSeconds(30), cts.Token);
            }

            secretOperator.Stop();
            return 0;
        }

        public static int Main(string[] args)
        {

            Console.WriteLine("Starting FileShare operator");
            return MainAsync().Result;
        }

        static IKubernetes GetK8sClient()
        {
            // load the k8s config from KUBECONFIG or $HOME/.kube/config or in-cluster if its available
            KubernetesClientConfiguration kubeConfig = Option.Maybe(Environment.GetEnvironmentVariable("KUBECONFIG"))
                .Else(() => Option.Maybe(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".kube", "config")))
                .Filter(File.Exists)
                .Map(path => KubernetesClientConfiguration.BuildConfigFromConfigFile(path))
                .GetOrElse(KubernetesClientConfiguration.InClusterConfig);

            return new Kubernetes(kubeConfig);
        }

        static IFileShareContoller GetFileShareController(IKubernetes client)
        {
            return new FileShareController(client);
        }

    }

    static class LinuxShutdownHandler
    {
        public static void Init(CancellationTokenSource cts)
        {
            void OnUnload(AssemblyLoadContext ctx) => CancelProgram();

            void CancelProgram()
            {
                Console.WriteLine("Termination requested, initiating shutdown.");
                cts.Cancel();
            }

            AssemblyLoadContext.Default.Unloading += OnUnload;
            Console.CancelKeyPress += (sender, cpe) => CancelProgram();
            Console.WriteLine("Waiting on shutdown handler to trigger");
        }
    }
}
