
namespace storage_operator.fileshare.pv
{
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using k8s.Models;

    class KubernetesPvByNameEqualityComparer : IEqualityComparer<V1PersistentVolume>
    {
        public bool Equals(V1PersistentVolume x, V1PersistentVolume y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (ReferenceEquals(x, null))
            {
                return false;
            }

            if (ReferenceEquals(y, null))
            {
                return false;
            }

            if (x.GetType() != y.GetType())
            {
                return false;
            }

            return x.Metadata?.Name == y.Metadata?.Name;
        }

        public int GetHashCode(V1PersistentVolume obj)
        {
            unchecked
            {
                int hashcode = obj.Metadata?.Name != null ? obj.Metadata.Name.GetHashCode() : 0;
                return hashcode;
            }
            throw new System.NotImplementedException();
        }
    }
}
