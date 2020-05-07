# Storage Operator

This project automatically creates persistent volumes from secret referencing 
Azure File shares. The idea would be to create a single secret per storage 
account, and the operator would create persistent volumes on the cluster based
on the file shares in the account.

The operator watches all secrets which have the label "fileshare=true", uses 
the secret data to access the Azure File shares, and creates a persistent volume
based on the file share name.

TODO:
- Secret label selector shouldn't be constant, it should be configurable.
- Persistent Volumes should also have configurable labels that can be applied.
- This only watches Secrets, should also be aware of Azure File changes.
- Set up CI/CD build for images.
