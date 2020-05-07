using k8s.Models;

namespace storage_operator.fileshare
{

    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;

    interface IFileShareContoller
    {
        Task ManageFileShareSecretAsync(V1Secret secret);

        Task DeleteFileShareSecretAsync(V1Secret secret);
    }
}
