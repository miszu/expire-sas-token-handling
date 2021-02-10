# Custom error message for expired Blob links

This is a proof of concept of showing a custom error message for expired links to blobs stored in Azure Blob Storage. 

It uses 2 Azure Function: the first one generates a link with SAS token valid for a given timespan. The link is then used as a parameter going into a second function, which in runtime checks whether the link is still valid: if yes, it redirects to the file, if not, it returns an HTML page with descriptive error.

Full article: https://miszu.medium.com/handling-expired-azure-blob-storage-links-in-a-user-friendly-way-1eb8d4d11a16

<img src="https://github.com/miszu/expiredSasTokenHandling/blob/main/lookAtMe.jpg?raw=true" width="700" height="400">
