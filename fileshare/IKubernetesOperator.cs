// Copyright (c) Microsoft. All rights reserved.

namespace storage_operator.fileshare
{
    using System;
    public interface IKubernetesOperator : IDisposable
    {
        void Start();

        void Stop();
    }
}
