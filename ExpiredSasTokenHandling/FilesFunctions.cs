using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Storage.Blob;
using System.Web;
using System.Linq;

namespace ExpiredSasTokenHandling
{
    // StorageAccountConnectionString should be a key in your Function App Configuration (or local.settings.json for local development)
    // It's value should contain a connection string to your Storage Account.
    [StorageAccount("StorageAccountConnectionString")]
    public static class ExpiredSasTokenHandling
    {
        private const string ContainerName = "YOUR_CONTAINER_NAME";
        private const string FilePath = "/YOUR/FILE.pdf";
        private const string FullFilePath = ContainerName + FilePath;

        [FunctionName("generateFileLink")]
        public static IActionResult GetLinkFunction(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)]
            HttpRequest request, [Blob(FullFilePath)] CloudBlobContainer container)
        {
            var accessPolicy = new SharedAccessBlobPolicy()
            {
                SharedAccessExpiryTime = DateTime.UtcNow.AddHours(1),
                Permissions = SharedAccessBlobPermissions.Read
            };

            var sasToken = container.GetSharedAccessSignature(accessPolicy);
            var authenticatedBlobLink = new Uri(container.Uri + FilePath + sasToken);

            var linkToFriendlyProxy = new UriBuilder
            {
                Scheme = request.Scheme,
                Host = request.Host.Host,
                Port = request.Host.Port.GetValueOrDefault(80),
                Path = $"api/{FriendyFileProxyFunctionName}",
            };

            var query = HttpUtility.ParseQueryString(linkToFriendlyProxy.Query);
            query[FriendyFileProxyUrlParameter] = authenticatedBlobLink.ToString();
            linkToFriendlyProxy.Query = query.ToString();

            return new OkObjectResult(linkToFriendlyProxy.ToString());
        }

        private const string FriendyFileProxyFunctionName = "fileProxy";
        private const string FriendyFileProxyUrlParameter = "originalUrl";
        private const string StorageHost = "YOUR_STORAGE_NAME.blob.core.windows.net";

        [FunctionName(FriendyFileProxyFunctionName)]
        public static IActionResult LinkProxyFunction(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)]
            HttpRequest request)
        {
            var blobUrlWithToken = request.Query.ContainsKey(FriendyFileProxyUrlParameter)
                ? request.Query[FriendyFileProxyUrlParameter][0]
                : null;

            if (!Uri.TryCreate(blobUrlWithToken, UriKind.Absolute, out var blobUrl))
            {
                return GetInvalidLinkHtml();
            }

            // You should only redirect to your own resources for safety (more info - 'Open redirect vulnerability')
            if (!string.Equals(blobUrl.Host, StorageHost, StringComparison.InvariantCultureIgnoreCase))
            {
                return GetInvalidLinkHtml();
            }

            // Show error if token's validity date is not there or is in the past
            var validityDateParameterValue = HttpUtility.ParseQueryString(blobUrl.Query).GetValues("se");
            if (validityDateParameterValue?.Any() != true ||
                !DateTime.TryParse(validityDateParameterValue.First(), out var validityDateTime) ||
                DateTime.UtcNow > validityDateTime.ToUniversalTime())
            {
                return GetInvalidLinkHtml();
            }

            return new RedirectResult(blobUrlWithToken);
        }

        private static IActionResult GetInvalidLinkHtml() => new ContentResult()
        {
            Content = $"<html><h3>This link is not valid anymore, please go back to the app and regenerate it. Contact support in case of trouble.</h3></html>",
            ContentType = "text/html"
        };
    }
}